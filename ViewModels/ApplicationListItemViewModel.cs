namespace AppGambit.ViewModels
{
    public class ApplicationListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? IconUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public int DownloadCount { get; set; }
        public string? Category { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public string UserDisplayName { get; set; } = string.Empty;
        public double AverageRating { get; set; }
        public int TotalRatings { get; set; }
        public int LikesCount { get; set; }
        public int DislikesCount { get; set; }
    }
}