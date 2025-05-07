// Функция автозаполнения имени на основе email
document.addEventListener('DOMContentLoaded', function() {
    // Элементы формы
    var emailInput = document.getElementById('emailInput');
    var displayNameInput = document.getElementById('displayNameInput');
    var registerForm = document.getElementById('registerForm');
    var registerButton = document.getElementById('registerButton');
    var registerSpinner = document.getElementById('registerSpinner');
    var googleAuthButton = document.getElementById('googleAuthButton');
    
    // Автозаполнение имени пользователя из email
    if (emailInput) {
        emailInput.addEventListener('blur', function() {
            if (displayNameInput && displayNameInput.value === '') {
                var email = emailInput.value;
                if (email && email.indexOf('@') !== -1) {
                    var username = email.substring(0, email.indexOf('@'));
                    if (username.length > 0) {
                        username = username.charAt(0).toUpperCase() + username.substring(1);
                        displayNameInput.value = username;
                    }
                }
            }
        });
    }
    
    // Показ индикатора загрузки при отправке формы
    if (registerForm) {
        registerForm.addEventListener('submit', function() {
            if (registerButton && registerSpinner) {
                registerButton.disabled = true;
                registerSpinner.classList.remove('d-none');
            }
        });
    }
    
    // Показ индикатора загрузки при нажатии на Google, НО без блокировки перехода
    if (googleAuthButton) {
        googleAuthButton.addEventListener('click', function() {
            // Просто добавляем спиннер, но не блокируем стандартное поведение
            var spinnerHtml = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Переход к Google...';
            this.innerHTML = spinnerHtml;
            this.classList.add('disabled');
            // Позволяем стандартному переходу по ссылке работать (не вызываем preventDefault)
        });
    }
}); 