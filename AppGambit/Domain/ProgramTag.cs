using System.ComponentModel.DataAnnotations.Schema;

namespace AppGambit.Domain
{
    public class ProgramTag
    {
        [ForeignKey("Program")]
        public int ProgramId { get; set; }

        [ForeignKey("Tag")]
        public int TagId { get; set; }

        // Navigation properties
        public virtual Program Program { get; set; } = null!;
        public virtual Tag Tag { get; set; } = null!;
    }
} 