using rapat_backend.DTOs.Rapat;
using rapat_backend.Helpers;
using rapat_backend.Repositories.Interfaces;
using rapat_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace rapat_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RapatController(
      IRapatRepository repo,
      IWebHostEnvironment env,
      IMicrosoftTeamsService teamsService,
      BlobStorageService blobStorageService) : ControllerBase
    {
        private readonly IRapatRepository _repo = repo;
        private readonly IWebHostEnvironment _env = env;
        private readonly IMicrosoftTeamsService _teamsService = teamsService;
        private readonly BlobStorageService _blobStorageService = blobStorageService;

        private const string ClaimNamaAkun = "namaakun";
        private const string InvalidSessionMessage = "Sesi tidak valid.";
        private const string OnlineMode = "Online";
        private const string RapatNotFoundMessage = "Rapat tidak ditemukan.";

        private string? GetCurrentUsername()
        {
            return User.FindFirstValue(ClaimNamaAkun)
                ?? User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.Identity?.Name;
        }

        [HttpGet("GetListRuangan")]
        public async Task<IActionResult> GetListRuangan()
        {
            try
            {
                var data = await _repo.GetListRuanganAsync();
                return Ok(new { data = data });
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpGet("GetListKaryawan")]
        public async Task<IActionResult> GetListKaryawan()
        {
            try
            {
                var data = await _repo.GetListKaryawanAsync();
                return Ok(new { data = data });
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        private async Task<(string EventId, string Link)> CreateTeamsLinkForRapatAsync(CreateRapatRequest dto)
        {
            try
            {
                var allEmails = new List<string>();

                if (dto.PesertaKaryawanIds != null && dto.PesertaKaryawanIds.Count > 0)
                {
                    var internalEmails = await _repo.GetEmailsByKaryawanIdsAsync(dto.PesertaKaryawanIds);
                    allEmails.AddRange(internalEmails);
                }

                if (dto.PesertaExternal != null)
                {
                    allEmails.AddRange(dto.PesertaExternal.Select(x => x.Email).Where(e => !string.IsNullOrEmpty(e))!);
                }

                var teamsResult = await _teamsService.CreateTeamsMeetingAsync(
                    dto.Judul,
                    dto.WaktuMulai,
                    dto.WaktuSelesai,
                    allEmails,
                    true,
                    "Microsoft Teams",
                    "Undangan Rapat Otomatis"
                );

                if (!string.IsNullOrEmpty(teamsResult) && teamsResult.Contains('|'))
                {
                    var parts = teamsResult.Split('|');
                    return (parts[0], parts.Length > 1 ? parts[1] : "");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARNING] Gagal generate link Teams saat Create: {ex.Message}");
            }
            return ("", "");
        }

        [HttpPost("CreateRapat")]
        public async Task<IActionResult> CreateRapat([FromBody] CreateRapatRequest dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var username = GetCurrentUsername();
            if (string.IsNullOrEmpty(username)) return Unauthorized(new { message = InvalidSessionMessage });

            try
            {
                if (dto.WaktuSelesai <= dto.WaktuMulai)
                {
                    return BadRequest(new { message = "Waktu selesai harus lebih besar dari waktu mulai." });
                }

                string eventIdToSave = "";
                var shouldAutoCreate =
                  (dto.Mode?.Equals(OnlineMode, StringComparison.OrdinalIgnoreCase) ?? false)
                  && (dto.AutoCreateTeamsLink || string.IsNullOrWhiteSpace(dto.Link));

                if (shouldAutoCreate)
                {
                    var (eventId, link) = await CreateTeamsLinkForRapatAsync(dto);
                    eventIdToSave = eventId;
                    if (!string.IsNullOrEmpty(link))
                    {
                        dto.Link = link;
                    }
                }

                var newId = await _repo.CreateAsync(dto, username);

                if (newId > 0 && !string.IsNullOrEmpty(eventIdToSave))
                {
                    await _repo.UpdateEventIdAsync(newId, eventIdToSave);
                }

                return Ok(new { message = "Draft Rapat berhasil disimpan.", id = newId, link = dto.Link });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private async Task<string?> UpdateTeamsMeetingForRapatAsync(UpdateRapatRequest dto, string eventId, string? modeClean)
        {
            try
            {
                var allEmails = new List<string>();

                if (dto.PesertaKaryawanIds != null && dto.PesertaKaryawanIds.Count > 0)
                {
                    var internalEmails = await _repo.GetEmailsByKaryawanIdsAsync(dto.PesertaKaryawanIds);
                    allEmails.AddRange(internalEmails);
                }

                if (dto.PesertaExternal != null && dto.PesertaExternal.Count > 0)
                {
                    var externalEmails = dto.PesertaExternal
                      .Where(x => !string.IsNullOrWhiteSpace(x.Email))
                      .Select(x => x.Email!)
                      .ToList();
                    allEmails.AddRange(externalEmails);
                }

                string lokasiName;
                if (modeClean != null && modeClean.Equals(OnlineMode, StringComparison.OrdinalIgnoreCase))
                {
                    lokasiName = "Microsoft Teams";
                }
                else
                {
                    lokasiName = await _repo.GetRuanganNameById(dto.RuanganId);
                }

                var newLink = await _teamsService.UpdateTeamsMeetingAsync(
                  eventId,
                  dto.Judul,
                  dto.WaktuMulai,
                  dto.WaktuSelesai,
                  allEmails,
                  OnlineMode.Equals(modeClean, StringComparison.OrdinalIgnoreCase),
                  lokasiName
                );

                if (OnlineMode.Equals(modeClean, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(newLink))
                {
                    return newLink;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Gagal update jadwal Teams: {ex.Message}");
            }
            return null;
        }

        [HttpPut("UpdateRapat")]
        public async Task<IActionResult> UpdateRapat([FromBody] UpdateRapatRequest dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var username = GetCurrentUsername();
            if (string.IsNullOrEmpty(username)) return Unauthorized(new { message = InvalidSessionMessage });

            try
            {
                var modeClean = dto.Mode?.Trim();
                if (!string.IsNullOrEmpty(modeClean) && modeClean.Equals("Offline", StringComparison.OrdinalIgnoreCase))
                {
                    dto.Link = null;
                }

                if (dto.WaktuSelesai <= dto.WaktuMulai)
                {
                    return BadRequest(new { message = "Waktu selesai harus lebih besar dari waktu mulai." });
                }

                var existingRapat = await _repo.GetDetailRapatAsync(dto.RapatId);

                if (existingRapat == null)
                    return NotFound(new { message = RapatNotFoundMessage });

                if (existingRapat.Status == "Terjadwal" && !string.IsNullOrEmpty(existingRapat.EventId))
                {
                    var updatedLink = await UpdateTeamsMeetingForRapatAsync(dto, existingRapat.EventId, modeClean);
                    if (updatedLink != null)
                    {
                        dto.Link = updatedLink;
                    }
                }

                var success = await _repo.UpdateAsync(dto, username);

                if (success)
                {
                    return Ok(new
                    {
                        message = "Data rapat berhasil diperbarui.",
                        teamsLink = dto.Link
                    });
                }
                else
                {
                    return NotFound(new { message = "Rapat tidak ditemukan atau gagal diupdate." });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("CreateMoM")]
        public async Task<IActionResult> CreateMoM([FromForm] CreateMoMRequest dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var username = GetCurrentUsername();
            if (string.IsNullOrEmpty(username)) return Unauthorized(new { message = InvalidSessionMessage });

            string? filePath = null;
            string? tempLocalPath = null;
            try
            {
                if (dto.FileDokumentasi != null && dto.FileDokumentasi.Length > 0)
                {
                    var tempFolder = Path.Combine(Path.GetTempPath(), "rapat_temp");
                    if (!Directory.Exists(tempFolder)) Directory.CreateDirectory(tempFolder);

                    var fileExtension = Path.GetExtension(dto.FileDokumentasi.FileName);
                    var tempFileName = $"{Guid.NewGuid()}{fileExtension}";
                    tempLocalPath = Path.Combine(tempFolder, tempFileName);

                    using (var stream = new FileStream(tempLocalPath, FileMode.Create))
                        await dto.FileDokumentasi.CopyToAsync(stream);

                    TryAddWatermarkToImage(tempLocalPath, dto.Tempat, dto.Tanggal);

                    filePath = await _blobStorageService.UploadFileAsync(tempLocalPath, dto.FileDokumentasi.ContentType ?? "application/octet-stream", "rapat");
                }

                var success = await _repo.CreateMoMAsync(dto, filePath ?? "", username);
                return success ? Ok(new { message = "Notulensi berhasil disimpan.", path = filePath }) : BadRequest(new { message = "Gagal simpan ke DB." });
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
            finally
            {
                if (!string.IsNullOrEmpty(tempLocalPath) && System.IO.File.Exists(tempLocalPath))
                {
                    try { System.IO.File.Delete(tempLocalPath); } catch {}
                }
            }
        }

#pragma warning disable CA1416
        private static void TryAddWatermarkToImage(string savePath, string? tempat, string? tanggal)
        {
            if (string.IsNullOrWhiteSpace(tempat) && string.IsNullOrWhiteSpace(tanggal))
                return;

            try
            {
                var extension = Path.GetExtension(savePath).ToLowerInvariant();
                if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
                    return;

                byte[] imageBytes = System.IO.File.ReadAllBytes(savePath);
                using (var ms = new System.IO.MemoryStream(imageBytes))
                {
                    using (var image = System.Drawing.Image.FromStream(ms))
                    {
                        using (var bitmap = new System.Drawing.Bitmap(image))
                        {
                            using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                            {
                                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                                float fontSize = Math.Max(12f, bitmap.Height * 0.025f);
                                using (var font = new System.Drawing.Font("Arial", fontSize, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel))
                                {
                                    var lines = new List<string>();
                                    if (!string.IsNullOrWhiteSpace(tempat)) lines.Add(tempat);
                                    if (!string.IsNullOrWhiteSpace(tanggal)) lines.Add(tanggal);

                                    string text = string.Join("\n", lines);
                                    var textSize = graphics.MeasureString(text, font);

                                    float x = 20f;
                                    float y = bitmap.Height - textSize.Height - 20f;

                                    using (var bgBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(140, 0, 0, 0)))
                                    {
                                        graphics.FillRectangle(bgBrush, x - 10f, y - 10f, textSize.Width + 20f, textSize.Height + 20f);
                                    }

                                    using (var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                                    {
                                        graphics.DrawString(text, font, textBrush, x, y);
                                    }
                                }
                            }

                            System.Drawing.Imaging.ImageFormat format = System.Drawing.Imaging.ImageFormat.Jpeg;
                            if (extension == ".png") format = System.Drawing.Imaging.ImageFormat.Png;
                            
                            bitmap.Save(savePath, format);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARNING] Gagal menyematkan watermark: {ex.Message}");
            }
        }
#pragma warning restore CA1416

        [HttpPost("GetAllRapat")]
        public async Task<IActionResult> GetAllRapat([FromBody] FilterRapatRequest filter)
        {
            var username = GetCurrentUsername();
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            try
            {
                var (data, totalData) = await _repo.GetAllByUserAsync(
                  username,
                  filter.Page,
                  filter.PageSize,
                  filter.Search,
                  filter.Status,
                  filter.Sort,
                  filter.Jenis ?? "",
                  filter.StartDate,
                  filter.EndDate
                );

                return Ok(new
                {
                    status = 200,
                    message = "Success retrieve data",
                    data = data,
                    totalData = totalData
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = 400, message = ex.Message });
            }
        }

        [HttpPost("CreateJenisRapat")]
        public async Task<IActionResult> CreateJenisRapat([FromBody] CreateJenisRapatRequest dto)
        {
            var username = GetCurrentUsername();
            if (string.IsNullOrEmpty(username))
                return Unauthorized(new { message = "Sesi tidak valid." });

            if (string.IsNullOrWhiteSpace(dto.NamaJenis))
                return BadRequest(new { message = "Nama Jenis Rapat wajib diisi." });

            try
            {
                var success = await _repo.CreateJenisRapatAsync(dto.NamaJenis, dto.Status ?? "Aktif");

                if (success)
                {
                    return Ok(new
                    {
                        status = 200,
                        message = "Jenis Rapat berhasil ditambahkan."
                    });
                }
                else
                {
                    return BadRequest(new { message = "Gagal menyimpan data." });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("UpdateJenisRapat")]
        public async Task<IActionResult> UpdateJenisRapat([FromBody] UpdateJenisRapatRequest dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.NamaJenis))
                {
                    return BadRequest(new { message = "Nama jenis rapat tidak boleh kosong." });
                }

                var result = await _repo.UpdateJenisRapatAsync(dto.Id, dto.NamaJenis, dto.Status ?? "Aktif");

                if (result)
                {
                    return Ok(new { message = "Jenis rapat berhasil diperbarui." });
                }

                return BadRequest(new { message = "Gagal memperbarui jenis rapat." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("DeleteJenisRapat/{id}")]
        public async Task<IActionResult> DeleteJenisRapat(int id)
        {
            try
            {
                var result = await _repo.DeleteJenisRapatAsync(id);

                if (result)
                {
                    return Ok(new { message = "Jenis rapat berhasil dihapus." });
                }

                return BadRequest(new { message = "Gagal menghapus jenis rapat." });
            }
            catch (Exception ex)
            {
                // Pesan error dari SQL (misal: Data sedang digunakan) akan muncul disini
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("ToggleStatusJenisRapat/{id}")]
        public async Task<IActionResult> ToggleStatusJenisRapat(int id)
        {
            try
            {
                var result = await _repo.ToggleStatusJenisRapatAsync(id);

                if (result)
                {
                    return Ok(new { message = "Status jenis rapat berhasil diubah." });
                }

                return BadRequest(new { message = "Gagal mengubah status jenis rapat." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("ExportExcel")]
        public async Task<IActionResult> ExportExcel([FromQuery] string Search = "", [FromQuery] string Status = "", [FromQuery] string Jenis = "", [FromQuery] DateTime? StartDate = null, [FromQuery] DateTime? EndDate = null)
        {
            var username = GetCurrentUsername();
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            try
            {
                var (dataRapat, _) = await _repo.GetAllByUserAsync(
                  username,
                  1,
                  int.MaxValue,
                  Search,
                  Status,
                  "rap_waktu_mulai desc",
                  Jenis,
                  StartDate,
                  EndDate
                );

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Laporan Rapat");

                    int headerRow = 4;
                    string[] headers = { "No", "Judul Rapat", "Jenis Rapat", "Waktu Mulai", "Waktu Selesai", "Mode", "Lokasi/Link", "Penyelenggara", "Status" };

                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = worksheet.Cells[headerRow, i + 1];
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                        cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                        cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    }

                    int row = headerRow + 1;
                    int no = 1;

                    foreach (var item in dataRapat)
                    {
                        worksheet.Cells[row, 1].Value = no++;
                        worksheet.Cells[row, 2].Value = item.Judul;

                        worksheet.Cells[row, 3].Value = !string.IsNullOrEmpty(item.Jenis) ? item.Jenis : "-";

                        worksheet.Cells[row, 4].Value = item.WaktuMulai.ToString("dd MMM yyyy HH:mm");
                        worksheet.Cells[row, 5].Value = item.WaktuSelesai.ToString("dd MMM yyyy HH:mm");

                        worksheet.Cells[row, 6].Value = item.Mode;

                        string lokasi;
                        if (OnlineMode.Equals(item.Mode, StringComparison.OrdinalIgnoreCase))
                        {
                            lokasi = string.IsNullOrEmpty(item.LinkMeeting) ? "Online Meeting" : item.LinkMeeting;
                        }
                        else
                        {
                            lokasi = item.RuanganNama;
                        }
                        worksheet.Cells[row, 7].Value = lokasi;

                        worksheet.Cells[row, 8].Value = item.PembuatNama;
                        worksheet.Cells[row, 9].Value = item.Status;

                        worksheet.Cells[row, 1, row, 9].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                        worksheet.Cells[row, 1, row, 9].Style.Border.Right.Style = ExcelBorderStyle.Thin;
                        worksheet.Cells[row, 1, row, 9].Style.Border.Left.Style = ExcelBorderStyle.Thin;

                        worksheet.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        worksheet.Cells[row, 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        worksheet.Cells[row, 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        worksheet.Cells[row, 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        worksheet.Cells[row, 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        worksheet.Cells[row, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                        row++;
                    }

                    worksheet.Cells["A1:I1"].Merge = true;
                    worksheet.Cells["A1"].Value = "DAFTAR KEGIATAN RAPAT";
                    worksheet.Cells["A1"].Style.Font.Size = 14;
                    worksheet.Cells["A1"].Style.Font.Bold = true;
                    worksheet.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    worksheet.Cells["A2:I2"].Merge = true;
                    string teksPeriode = (StartDate.HasValue && EndDate.HasValue)
                      ? $"Periode: {StartDate.Value:dd/MM/yyyy} s/d {EndDate.Value:dd/MM/yyyy}"
                      : "Periode: Semua Waktu";
                    worksheet.Cells["A2"].Value = teksPeriode;
                    worksheet.Cells["A2"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    worksheet.Cells["A3:I3"].Merge = true;
                    worksheet.Cells["A3"].Value = $"Dicetak Oleh: {username} pada {DateTime.Now:dd MMM yyyy HH:mm}";
                    worksheet.Cells["A3"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    worksheet.Column(1).Width = 5;
                    worksheet.Column(2).Width = 35;
                    worksheet.Column(3).Width = 25;
                    worksheet.Column(4).Width = 20;
                    worksheet.Column(5).Width = 20;
                    worksheet.Column(6).Width = 15;
                    worksheet.Column(7).Width = 30;
                    worksheet.Column(8).Width = 25;
                    worksheet.Column(9).Width = 15;

                    var namaPeserta = User.FindFirstValue(ClaimTypes.Name) ?? username ?? "User";
                    string cleanName = Regex.Replace(namaPeserta, "[^a-zA-Z0-9]", "_");
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                    var fileName = $"Laporan_Rapat_Selesai_{cleanName}_{timestamp}.xlsx";
                    var content = await package.GetAsByteArrayAsync();

                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Gagal export excel: " + ex.Message });
            }
        }

        [HttpGet("DetailRapat/{id}")]
        public async Task<IActionResult> Detail(int id)
        {
            try
            {
                var username = GetCurrentUsername();
                var result = await _repo.GetByIdAsync(id, username);

                if (result == null) return NotFound(new { message = RapatNotFoundMessage });
                return Ok(new { data = result });
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("EditStatusItemAksi")]
        public async Task<IActionResult> EditStatusItemAksi([FromForm] UpdateStatusItemAksiRequest dto)
        {
            var username = GetCurrentUsername();
            if (string.IsNullOrEmpty(username)) return Unauthorized(new { message = InvalidSessionMessage });

            string? filePath = null;
            try
            {
                if (dto.FileBukti != null && dto.FileBukti.Length > 0)
                {
                    filePath = await _blobStorageService.UploadFileAsync(dto.FileBukti, "tindaklanjut");
                }

                var success = await _repo.UpdateStatusItemAksiAsync(dto.TindakLanjutId, dto.StatusBaru, filePath, username);

                return success
                  ? Ok(new { message = "Status tindak lanjut diperbarui.", path = filePath })
                  : BadRequest(new { message = "Gagal memperbarui status." });
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        private async Task<(string? LinkToSave, string Message, IActionResult? ActionResult)> TransitionToScheduledStatusAsync(int id)
        {
            var result = await HandleScheduledStatusAsync(id);

            if (result.IsPartialSuccess)
            {
                return (null, "", new OkObjectResult(new { message = result.Message, isPartialSuccess = true }));
            }

            string eventId = "";
            string realLink = "";

            if (!string.IsNullOrEmpty(result.Link))
            {
                if (result.Link.Contains('|'))
                {
                    var parts = result.Link.Split('|');
                    eventId = parts[0];
                    if (parts.Length > 1)
                        realLink = parts[1];
                }
                else
                    realLink = result.Link;
            }

            if (!string.IsNullOrEmpty(eventId))
                await _repo.UpdateEventIdAsync(id, eventId);

            return (realLink, "Undangan Rapat berhasil dikirim.", null);
        }

        private async Task TransitionToCancelledStatusAsync(string? eventId, string judul)
        {
            if (!string.IsNullOrEmpty(eventId))
                await _teamsService.CancelTeamsMeetingAsync(eventId, judul);
        }

        [HttpPut("UpdateStatusRapat/{id}")]
        public async Task<IActionResult> UpdateStatusRapat(int id, [FromBody] UpdateStatusRapatRequest dto)
        {
            var username = GetCurrentUsername();
            if (string.IsNullOrEmpty(username)) return Unauthorized(new { message = InvalidSessionMessage });

            try
            {
                var existingRapat = await _repo.GetDetailRapatAsync(id);
                if (existingRapat == null) return NotFound(new { message = RapatNotFoundMessage });

                string? linkToSave = null;
                string message = "Status berhasil diperbarui.";

                if (dto.Status.Equals("Terjadwal", StringComparison.OrdinalIgnoreCase))
                {
                    var transitionResult = await TransitionToScheduledStatusAsync(id);
                    if (transitionResult.ActionResult != null)
                    {
                        return transitionResult.ActionResult;
                    }
                    linkToSave = transitionResult.LinkToSave;
                    message = transitionResult.Message;
                }
                else if (dto.Status.Equals("Dibatalkan", StringComparison.OrdinalIgnoreCase)
                      || dto.Status.Equals("Tolak", StringComparison.OrdinalIgnoreCase))
                {
                    await TransitionToCancelledStatusAsync(existingRapat.EventId, existingRapat.Judul);
                    message = "Rapat telah dibatalkan.";
                    linkToSave = null;
                }
                else
                {
                    linkToSave = existingRapat.LinkMeeting;
                }

                var success = await _repo.UpdateStatusAsync(id, dto.Status, username, linkToSave);

                if (!success) return NotFound(new { message = "Gagal update status di database." });

                return Ok(new { message = message, link = linkToSave });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR UpdateStatusRapat] {ex.Message}");
                return BadRequest(new { message = ex.Message });
            }
        }

        private async Task<(string? Link, string Message, bool IsPartialSuccess)> HandleScheduledStatusAsync(int rapatId)
        {
            try
            {
                var rapatDetail = await _repo.GetDetailRapatAsync(rapatId);
                if (rapatDetail == null) return (null, RapatNotFoundMessage, false);

                var participantEmails = await _repo.GetEmailsByKaryawanIdsAsync(
                    rapatDetail.Peserta
                    .Where(p => !p.IsExternal && !string.IsNullOrEmpty(p.KaryawanId))
                    .Select(p => p.KaryawanId!).ToList()
                );

                if (rapatDetail.Peserta.Any(p => p.IsExternal))
                {
                    participantEmails.AddRange(rapatDetail.Peserta
                        .Where(p => p.IsExternal && !string.IsNullOrEmpty(p.Email))
                        .Select(p => p.Email!)
                    );
                }

                bool isOnline = OnlineMode.Equals(rapatDetail.Mode, StringComparison.OrdinalIgnoreCase);
                string locationName = isOnline ? "Microsoft Teams" : rapatDetail.RuanganNama;

                var teamsResponse = await _teamsService.CreateTeamsMeetingAsync(
                    rapatDetail.Judul,
                    rapatDetail.WaktuMulai,
                    rapatDetail.WaktuSelesai,
                    participantEmails,
                    isOnline,
                    locationName,
                    "Undangan resmi rapat."
                );

                return (teamsResponse, "Status menjadi Terjadwal & Undangan telah dikirim.", false);
            }
            catch (Exception ex)
            {
                return (null, "Gagal mengirim undangan Teams: " + ex.Message, true);
            }
        }

        [HttpPut("UpdateAbsensi")]
        public async Task<IActionResult> UpdateAbsensi([FromBody] UpdateAbsensiRapatRequest dto)
        {
            var username = GetCurrentUsername();
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            try
            {
                var success = await _repo.UpdateAbsensiAsync(dto.RapatId, dto.KaryawanId, dto.Email, dto.StatusHadir, dto.Keterangan, dto.PeranBaru, username);

                return Ok(new
                {
                    success = success,
                    message = success ? "Absensi diperbarui." : "Tidak ada perubahan data atau data tidak ditemukan."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("CreateItemAksi")]
        public async Task<IActionResult> CreateItemAksi([FromBody] CreateItemAksiRequest dto)
        {
            var username = GetCurrentUsername();
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            try
            {
                var success = await _repo.CreateItemAksiAsync(dto, username);
                return success ? Ok(new { message = "Item aksi ditambahkan." }) : BadRequest();
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpDelete("DeleteItemAksi/{id}")]
        public async Task<IActionResult> DeleteItemAksi(int id)
        {
            try
            {
                var result = await _repo.DeleteItemAksiAsync(id);

                if (!result)
                {
                    return NotFound(new { message = "Item aksi tidak ditemukan atau sudah dihapus." });
                }

                return Ok(new { message = "Berhasil menghapus item aksi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Terjadi kesalahan server: " + ex.Message });
            }
        }

        [HttpPost("CancelRapat/{id}")]
        public async Task<IActionResult> CancelRapat(int id)
        {
            var username = GetCurrentUsername();
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            try
            {
                var existingRapat = await _repo.GetDetailRapatAsync(id);
                if (existingRapat == null) return NotFound(new { message = RapatNotFoundMessage });

                if (!string.IsNullOrEmpty(existingRapat.EventId))
                {
                    try
                    {
                        bool isTeamsCancelled = await _teamsService.CancelTeamsMeetingAsync(
                          existingRapat.EventId,
                          existingRapat.Judul
                        );

                        if (!isTeamsCancelled)
                        {
                            return BadRequest(new { message = "Gagal update status di Teams.", eventId = existingRapat.EventId });
                        }
                    }
                    catch (Exception msGraphError)
                    {
                        return StatusCode(500, new { message = "Error Microsoft Graph:", detail = msGraphError.Message });
                    }
                }

                var success = await _repo.CancelAsync(id, username);

                return success
                  ? Ok(new { message = "Rapat berhasil dibatalkan (Status updated di Outlook)." })
                  : NotFound(new { message = "Gagal update database." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("GetListJenisRapat")]
        public async Task<IActionResult> GetListJenisRapat()
        {
            try
            {
                var data = await _repo.GetListJenisRapatAsync();
                return Ok(new { data = data });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}