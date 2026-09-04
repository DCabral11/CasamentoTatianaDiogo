using System.ComponentModel.DataAnnotations;

namespace CasamentoTatianaDiogo.Models
{
    public class WeddingSettings
    {
        public int Id { get; set; }

        public DateTime? WeddingDateTime { get; set; }

        [StringLength(200)] 
        public string? CeremonyLocationName { get; set; }
        
        [StringLength(500)] 
        public string? CeremonyAddress { get; set; }
        
        [StringLength(1000)] 
        public string? CeremonyGoogleMapsUrl { get; set; }
        
        [StringLength(2000)] 
        public string? CeremonyGoogleMapsEmbedUrl { get; set; }
        
        [StringLength(200)] 
        public string? ReceptionLocationName { get; set; }
        
        [StringLength(500)] 
        public string? ReceptionAddress { get; set; }
        
        [StringLength(1000)] 
        public string? ReceptionGoogleMapsUrl { get; set; }
        
        [StringLength(2000)] 
        public string? ReceptionGoogleMapsEmbedUrl { get; set; }
        
        [StringLength(120)] 
        public string BrideName { get; set; } = "Tatiana";
        
        [StringLength(50)] 
        public string? BridePhone { get; set; }
        
        [StringLength(200)] 
        public string? BrideEmail { get; set; }
        
        [StringLength(120)] 
        public string GroomName { get; set; } = "Diogo";
        
        [StringLength(50)] 
        public string? GroomPhone { get; set; }
        
        [StringLength(200)] 
        public string? GroomEmail { get; set; }
        
        public string? CoupleStory { get; set; }
        
        [StringLength(200)] 
        public string? HomeHeroTitle { get; set; } = "Estamos a casar!";
        
        [StringLength(300)] 
        public string? HomeHeroSubtitle { get; set; } = "Junta-te a nós para celebrar este dia especial.";
        
        [StringLength(300)] 
        public string LogoPath { get; set; } = "/images/navbar.png";
        
        [StringLength(20)] 
        public string PrimaryColor { get; set; } = "#bd4742";
        
        [StringLength(20)] 
        public string SecondaryColor { get; set; } = "#fce8e4";
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
