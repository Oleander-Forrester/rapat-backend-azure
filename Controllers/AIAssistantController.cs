using Microsoft.AspNetCore.Mvc;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NAudio.Wave;

namespace rapat_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AIAssistantController(IConfiguration configuration, IHttpClientFactory httpClientFactory) : ControllerBase
    {
        private readonly IConfiguration _configuration = configuration;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

        [HttpPost("transcribe-and-summarize")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = int.MaxValue, ValueLengthLimit = int.MaxValue)]
        public async Task<IActionResult> TranscribeAndSummarize(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Audio file is required" });

            // 1. Save temp file
            var tempInputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".m4a");
            using (var stream = new FileStream(tempInputPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            try
            {
                var speechKey = _configuration["Azure:SpeechService:SubscriptionKey"];
                var speechRegion = _configuration["Azure:SpeechService:Region"];

                if (string.IsNullOrEmpty(speechKey))
                {
                    return BadRequest(new { message = "Azure Speech API Key is not configured." });
                }

                var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
                speechConfig.SpeechRecognitionLanguage = "id-ID";
                speechConfig.SetProperty(PropertyId.SpeechServiceConnection_InitialSilenceTimeoutMs, "5000");
                speechConfig.SetProperty(PropertyId.SpeechServiceConnection_EndSilenceTimeoutMs, "1000");

                // Setup Push Stream untuk Azure Speech
                var speechFormat = AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1);
                using var pushStream = AudioInputStream.CreatePushStream(speechFormat);
                using var audioConfig = AudioConfig.FromStreamInput(pushStream);
                using var speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);

                // Gunakan Continuous Recognition agar bisa transkripsi percakapan panjang
                var transcriptBuilder = new StringBuilder();
                var recognitionDone = new TaskCompletionSource<bool>();

                speechRecognizer.Recognized += (s, e) =>
                {
                    if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrWhiteSpace(e.Result.Text))
                    {
                        transcriptBuilder.Append(e.Result.Text + " ");
                    }
                };
                speechRecognizer.SessionStopped += (s, e) => recognitionDone.TrySetResult(true);
                speechRecognizer.Canceled += (s, e) => recognitionDone.TrySetResult(false);

                await speechRecognizer.StartContinuousRecognitionAsync();

                // Kirim semua data audio ke push stream
                using (var reader = new MediaFoundationReader(tempInputPath))
                {
                    var outFormat = new WaveFormat(16000, 16, 1);
                    using (var resampler = new MediaFoundationResampler(reader, outFormat))
                    {
                        byte[] buffer = new byte[3200];
                        int bytesRead;
                        while ((bytesRead = resampler.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            pushStream.Write(buffer, bytesRead);
                        }
                    }
                }
                pushStream.Close(); // Signal: audio selesai

                // Tunggu hingga session selesai (max 30 menit agar audio panjang tidak terputus)
                await Task.WhenAny(recognitionDone.Task, Task.Delay(1800000));
                await speechRecognizer.StopContinuousRecognitionAsync();

                string transcript = transcriptBuilder.ToString().Trim();

                if (string.IsNullOrWhiteSpace(transcript))
                {
                    return BadRequest(new { message = "Tidak ada suara yang berhasil dikenali. Pastikan mikrofon aktif dan suara cukup jelas." });
                }

                // 3. Buat Ringkasan menggunakan Azure OpenAI
                var openaiEndpoint = _configuration["Azure:OpenAI:Endpoint"];
                var openaiKey = _configuration["Azure:OpenAI:ApiKey"];
                var deploymentName = _configuration["Azure:OpenAI:DeploymentName"];

                string summary = "Ringkasan tidak tersedia.";
                
                if (!string.IsNullOrEmpty(openaiKey) && openaiKey != "MASUKKAN_API_KEY_OPENAI_DISINI" && !string.IsNullOrEmpty(openaiEndpoint))
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    httpClient.DefaultRequestHeaders.Add("api-key", openaiKey);

                    var url = $"{openaiEndpoint.TrimEnd('/')}/openai/deployments/{deploymentName}/chat/completions?api-version=2024-08-01-preview";

                    var systemPrompt = "Anda adalah asisten notulensi rapat profesional. " +
                        "Tugas Anda adalah membuat ringkasan notulensi rapat yang terstruktur berdasarkan transkrip percakapan yang diberikan. " +
                        "Buat ringkasan dalam format: poin-poin utama yang dibahas, keputusan yang diambil (jika ada), dan tindak lanjut yang perlu dilakukan (jika ada). " +
                        "Gunakan bahasa Indonesia yang formal. " +
                        "PENTING: Rangkum HANYA berdasarkan isi transkrip yang diberikan. Jangan menambahkan informasi dari luar.";

                    var body = new
                    {
                        messages = new[]
                        {
                            new { role = "system", content = systemPrompt },
                            new { role = "user", content = $"Berikut adalah transkrip rekaman rapat:\n\n{transcript}\n\nBuatkan ringkasan notulensi dari transkrip di atas." }
                        },
                        max_tokens = 1500,
                        temperature = 0.3
                    };

                    var json = JsonSerializer.Serialize(body);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    
                    var httpResponse = await httpClient.PostAsync(url, content);
                    var responseBody = await httpResponse.Content.ReadAsStringAsync();
                    
                    Console.WriteLine($"OpenAI Response ({httpResponse.StatusCode}): {responseBody}");

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        var jsonDoc = JsonDocument.Parse(responseBody);
                        summary = jsonDoc.RootElement
                            .GetProperty("choices")[0]
                            .GetProperty("message")
                            .GetProperty("content")
                            .GetString() ?? "Ringkasan tidak tersedia.";
                    }
                    else
                    {
                        summary = $"[Gagal membuat ringkasan: {httpResponse.StatusCode} - {responseBody}]";
                    }
                }

                return Ok(new
                {
                    transcript = transcript,
                    summary = summary
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("AI ERROR: " + ex.ToString());
                return StatusCode(500, new { message = ex.Message, detail = ex.StackTrace });
            }
            finally
            {
                if (System.IO.File.Exists(tempInputPath))
                {
                    System.IO.File.Delete(tempInputPath);
                }
            }
        }
    }
}
