namespace rapat_backend.DTOs.Rapat
{
    public class UpdateJenisRapatRequest
    {
        public int Id { get; set; }
        public string NamaJenis { get; set; } = string.Empty;
        public string Status { get; set; } = "Aktif";
    }
}