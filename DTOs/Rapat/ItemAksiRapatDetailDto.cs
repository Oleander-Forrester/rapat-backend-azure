using System.Text.Json.Serialization;

namespace rapat_backend.DTOs.Rapat
{
    public class ItemAksiRapatDetailDto
    {
        public int Id { get; set; }
        public string Deskripsi { get; set; } = string.Empty;

        [JsonPropertyName("PIC_Nama")]
        public string PIC_Nama { get; set; } = string.Empty;

        public string PIC_KaryawanId { get; set; } = string.Empty;

        public string? PIC_Username { get; set; }
        public bool IsCurrentUserPIC { get; set; }
        
        public string? FileBukti { get; set; }
        public string? ModifBy { get; set; }
        public DateTime? ModifDate { get; set; }
        public DateTime Deadline { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
