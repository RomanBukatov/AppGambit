namespace AppGambit.Models
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
        
        // Добавляем свойства для подробной диагностики
        public string? ErrorMessage { get; set; }
        public string? Query { get; set; }
        public string? Path { get; set; }
        public string? Cookies { get; set; }
        
        // Показывать подробную информацию только если есть сообщение об ошибке
        public bool ShowDetailedError => !string.IsNullOrEmpty(ErrorMessage);
    }
}
