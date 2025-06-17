using AppGambit.Models;

namespace AppGambit.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalApplications { get; set; }
        public int TotalComments { get; set; }
        public int TotalRatings { get; set; }
        public List<ApplicationListItemViewModel> RecentApplications { get; set; } = new();
        public List<AdminCommentViewModel> RecentComments { get; set; } = new();
    }

    public class AdminApplicationViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int DownloadCount { get; set; }
        public string UserDisplayName { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public int CommentsCount { get; set; }
    }

    public class AdminCommentViewModel
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string UserDisplayName { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string ApplicationName { get; set; } = string.Empty;
        public int ApplicationId { get; set; }
    }

    public class AdminUserViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int ApplicationsCount { get; set; }
        public int CommentsCount { get; set; }
    }
}