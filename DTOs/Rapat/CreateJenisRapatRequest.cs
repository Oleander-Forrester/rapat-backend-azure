namespace rapat_backend.DTOs.Rapat
{
    public class CreateJenisRapatRequest
    {
        public string NamaJenis { get; set; } = string.Empty;
        public string Status { get; set; } = "Aktif";
    }
}