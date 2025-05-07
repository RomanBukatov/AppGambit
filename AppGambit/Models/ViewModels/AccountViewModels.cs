using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace AppGambit.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Поле Email обязательно для заполнения")]
        [EmailAddress(ErrorMessage = "Неверный формат Email")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Поле Имя пользователя обязательно для заполнения")]
        [Display(Name = "Имя пользователя")]
        [StringLength(50, ErrorMessage = "Имя пользователя должно содержать от {2} до {1} символов", MinimumLength = 3)]
        public string DisplayName { get; set; }

        [Required(ErrorMessage = "Поле Пароль обязательно для заполнения")]
        [StringLength(100, ErrorMessage = "Пароль должен содержать минимум {2} символов", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Пароль")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Подтверждение пароля")]
        [Compare("Password", ErrorMessage = "Пароли не совпадают")]
        public string ConfirmPassword { get; set; }
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "Поле Email обязательно для заполнения")]
        [EmailAddress(ErrorMessage = "Неверный формат Email")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Поле Пароль обязательно для заполнения")]
        [DataType(DataType.Password)]
        [Display(Name = "Пароль")]
        public string Password { get; set; }

        [Display(Name = "Запомнить меня")]
        public bool RememberMe { get; set; }
    }

    public class ExternalLoginViewModel
    {
        [Required(ErrorMessage = "Поле Email обязательно для заполнения")]
        [EmailAddress(ErrorMessage = "Неверный формат Email")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Имя пользователя")]
        [StringLength(50, ErrorMessage = "Имя пользователя должно содержать от {2} до {1} символов", MinimumLength = 3)]
        public string DisplayName { get; set; }
    }

    public class ProfileViewModel
    {
        [Display(Name = "ID пользователя")]
        public string UserId { get; set; }

        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Поле Имя пользователя обязательно для заполнения")]
        [Display(Name = "Имя пользователя")]
        [StringLength(50, ErrorMessage = "Имя пользователя должно содержать от {2} до {1} символов", MinimumLength = 3)]
        public string DisplayName { get; set; }

        [Display(Name = "Биография")]
        [StringLength(5000)]
        public string? Bio { get; set; }

        [Display(Name = "Аватар")]
        [StringLength(255)]
        [DataType(DataType.Url)]
        public string? AvatarUrl { get; set; }

        [Display(Name = "Сайт")]
        [StringLength(255)]
        [DataType(DataType.Url)]
        public string? WebsiteUrl { get; set; }

        [Display(Name = "Местоположение")]
        [StringLength(100)]
        public string? Location { get; set; }

        [Display(Name = "Дата регистрации")]
        [DataType(DataType.Date)]
        public DateTime RegistrationDate { get; set; }
        
        // Флаг, указывающий, является ли просматриваемый профиль профилем текущего пользователя
        public bool IsCurrentUser { get; set; }
    }
} 