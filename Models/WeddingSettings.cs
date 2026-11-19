using System.ComponentModel.DataAnnotations;

namespace CasamentoTatianaDiogo.Models
{
    public class WeddingSettings
    {
        public WeddingSettings CopyForPresentation() => (WeddingSettings)MemberwiseClone();
        public int Id { get; set; }

        #region Dealines Properties

        public DateTime? WeddingDateTime { get; set; }

        public DateTime? WeddingEndDateTime { get; set; }

        [Display(Name = "Data Limite para Confirmar Presença")]
        public DateOnly? RsvpDeadline { get; set; }

        #endregion

        #region Venues Properties

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

        #endregion

        #region Bride & Groom Properties

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

        #endregion

        #region Contacts' Format Properties

        [Required, StringLength(2)] 
        public string PhoneRegion { get; set; } = "PT";
        
        [Required, RegularExpression("international|compact|national")] 
        public string PhoneDisplay { get; set; } = "international";
        
        [Required, RegularExpression(@"ext\.|ramal|x")] 
        public string ExtensionLabel { get; set; } = "ext.";
        
        [Required, RegularExpression("unicode|ascii")] 
        public string EmailDomainDisplay { get; set; } = "unicode";
        
        [StringLength(200)] 
        public string? DefaultEmailDomain { get; set; }

        #endregion

        #region General Settings Properties

        public string? CoupleStory { get; set; }

        [StringLength(200)]
        public string? HomeHeroTitle { get; set; } = "Estamos a casar!";

        [StringLength(300)]
        public string? HomeHeroSubtitle { get; set; } = "Junta-te a nós para celebrar este dia especial.";

        [StringLength(300)]
        public string LogoPath { get; set; } = "/images/navbar.png";

        [StringLength(500)]
        public string? SpotifyTrackUrl { get; set; }

        [StringLength(20)]
        [RegularExpression("^#[a-fA-F0-9]{6}$", ErrorMessage = "Escolhe uma cor principal válida.")]
        public string PrimaryColor { get; set; } = "#bd4742";

        [StringLength(20)]
        [RegularExpression("^#[a-fA-F0-9]{6}$", ErrorMessage = "Escolhe uma cor de fundo válida.")]
        public string SecondaryColor { get; set; } = "#fce8e4";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        #endregion
    }
}
