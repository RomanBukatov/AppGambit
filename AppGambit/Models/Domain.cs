using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AppGambit.Models
{
    // Делаем классы домена доступными для ссылок из Models
    // Это позволит решить проблему кольцевой зависимости и доступности
    
    // Класс UserProfile
    public class UserProfile
    {
        [Key]
        [ForeignKey("User")]
        public string UserId { get; set; } = null!;

        [StringLength(100)]
        public string? DisplayName { get; set; }

        [StringLength(255)]
        public string? AvatarUrl { get; set; }

        public string? Bio { get; set; }

        [StringLength(255)]
        public string? WebsiteUrl { get; set; }

        [StringLength(100)]
        public string? Location { get; set; }

        // Navigation property
        public virtual ApplicationUser User { get; set; } = null!;
    }

    // Класс Category
    public class Category
    {
        [Key]
        public int CategoryId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public int? ParentCategoryId { get; set; }

        // Self-reference navigation properties
        [ForeignKey("ParentCategoryId")]
        public virtual Category? ParentCategory { get; set; }
        public virtual ICollection<Category> SubCategories { get; set; } = new List<Category>();

        // Navigation property for programs
        public virtual ICollection<SoftwareProgram> Programs { get; set; } = new List<SoftwareProgram>();
    }

    // Класс Program переименован в SoftwareProgram
    public class SoftwareProgram
    {
        [Key]
        public int ProgramId { get; set; }

        [Required]
        [StringLength(255)]
        public string Title { get; set; } = null!;

        [Required]
        public string Description { get; set; } = null!;

        [Required]
        public DateTime UploadDate { get; set; }

        [StringLength(50)]
        public string? Version { get; set; }

        public long? FileSize { get; set; }

        [Required]
        [StringLength(255)]
        public string DownloadUrl { get; set; } = null!;

        public string? AuthorId { get; set; }

        public double? AverageRating { get; set; }

        public int? CategoryId { get; set; }

        // Navigation properties
        [ForeignKey("AuthorId")]
        public virtual ApplicationUser? Author { get; set; }

        [ForeignKey("CategoryId")]
        public virtual Category? Category { get; set; }

        public virtual ICollection<Screenshot> Screenshots { get; set; } = new List<Screenshot>();
        public virtual ICollection<ProgramTag> ProgramTags { get; set; } = new List<ProgramTag>();
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();
        public virtual ICollection<Download> Downloads { get; set; } = new List<Download>();
        public virtual ICollection<ProgramSubscription> Subscriptions { get; set; } = new List<ProgramSubscription>();
    }

    // Класс Screenshot
    public class Screenshot
    {
        [Key]
        public int ScreenshotId { get; set; }

        [Required]
        public int ProgramId { get; set; }

        [Required]
        [StringLength(255)]
        public string ImageUrl { get; set; } = null!;

        [Required]
        public int Order { get; set; }

        // Navigation property
        [ForeignKey("ProgramId")]
        public virtual SoftwareProgram Program { get; set; } = null!;
    }

    // Класс Tag
    public class Tag
    {
        [Key]
        public int TagId { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = null!;

        // Navigation property
        public virtual ICollection<ProgramTag> ProgramTags { get; set; } = new List<ProgramTag>();
    }

    // Класс ProgramTag
    public class ProgramTag
    {
        [ForeignKey("Program")]
        public int ProgramId { get; set; }

        [ForeignKey("Tag")]
        public int TagId { get; set; }

        // Navigation properties
        public virtual SoftwareProgram Program { get; set; } = null!;
        public virtual Tag Tag { get; set; } = null!;
    }

    // Класс Comment
    public class Comment
    {
        [Key]
        public int CommentId { get; set; }

        [Required]
        public int ProgramId { get; set; }

        [Required]
        public string UserId { get; set; } = null!;

        public int? ParentCommentId { get; set; }

        [Required]
        public string Content { get; set; } = null!;

        [Required]
        public DateTime CommentDate { get; set; }

        [Required]
        public bool IsApproved { get; set; } = true;

        [Required]
        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedDate { get; set; }

        public string? DeletedByUserId { get; set; }

        // Navigation properties
        [ForeignKey("ProgramId")]
        public virtual SoftwareProgram Program { get; set; } = null!;

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } = null!;

        [ForeignKey("ParentCommentId")]
        public virtual Comment? ParentComment { get; set; }
        public virtual ICollection<Comment> Replies { get; set; } = new List<Comment>();

        [ForeignKey("DeletedByUserId")]
        public virtual ApplicationUser? DeletedByUser { get; set; }

        public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();
    }

    // Класс Rating
    public class Rating
    {
        [Key]
        public int RatingId { get; set; }

        public int? ProgramId { get; set; }

        public int? CommentId { get; set; }

        [Required]
        public string UserId { get; set; } = null!;

        [Required]
        [StringLength(20)]
        public string RatingType { get; set; } = null!; // 'Like' or 'Dislike'

        [Required]
        public DateTime RatingDate { get; set; }

        // Navigation properties
        [ForeignKey("ProgramId")]
        public virtual SoftwareProgram? Program { get; set; }

        [ForeignKey("CommentId")]
        public virtual Comment? Comment { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } = null!;
    }

    // Класс Download
    public class Download
    {
        [Key]
        public int DownloadId { get; set; }

        [Required]
        public int ProgramId { get; set; }

        public string? UserId { get; set; }

        [Required]
        public DateTime DownloadDate { get; set; }

        [StringLength(50)]
        public string? IpAddress { get; set; }

        // Navigation properties
        [ForeignKey("ProgramId")]
        public virtual SoftwareProgram Program { get; set; } = null!;

        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }
    }

    // Класс Notification
    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }

        [Required]
        public string UserId { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string NotificationType { get; set; } = null!; // 'NewCommentReply', 'ProgramUpdate', etc.

        [Required]
        public string Message { get; set; } = null!;

        [StringLength(255)]
        public string? Link { get; set; }

        [Required]
        public DateTime CreationDate { get; set; }

        [Required]
        public bool IsRead { get; set; } = false;

        // Navigation property
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } = null!;
    }

    // Класс ProgramSubscription
    public class ProgramSubscription
    {
        [Key]
        public int SubscriptionId { get; set; }

        [Required]
        public string UserId { get; set; } = null!;

        [Required]
        public int ProgramId { get; set; }

        [Required]
        public DateTime SubscriptionDate { get; set; }

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } = null!;

        [ForeignKey("ProgramId")]
        public virtual SoftwareProgram Program { get; set; } = null!;
    }

    // Класс AboutCreatorInfo
    public class AboutCreatorInfo
    {
        [Key]
        public int InfoId { get; set; } = 1;

        [Required]
        [StringLength(100)]
        public string CreatorName { get; set; } = null!;

        [StringLength(255)]
        public string? ContactEmail { get; set; }

        [StringLength(50)]
        public string? ContactPhone { get; set; }

        [StringLength(255)]
        public string? WebsiteUrl { get; set; }

        public string? AboutText { get; set; }

        [Required]
        public DateTime LastUpdated { get; set; }
    }
} 