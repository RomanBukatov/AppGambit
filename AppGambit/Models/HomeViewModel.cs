using System.Collections.Generic;
using AppGambit.Models;

namespace AppGambit.Models
{
    public class HomeViewModel
    {
        public IEnumerable<SoftwareProgram> Programs { get; set; } = new List<SoftwareProgram>();
        public IEnumerable<Tag> PopularTags { get; set; } = new List<Tag>();
        public string SearchTerm { get; set; }
        public string SelectedTag { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }
} 