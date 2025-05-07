// Функции для страницы входа
document.addEventListener('DOMContentLoaded', function() {
    // Элементы формы
    var loginForm = document.getElementById('loginForm');
    var loginButton = document.getElementById('loginButton');
    var loginSpinner = document.getElementById('loginSpinner');
    var googleAuthForm = document.getElementById('googleAuthForm');
    var googleAuthButton = document.getElementById('googleAuthButton');
    
    // Показать индикатор загрузки при входе
    if (loginForm) {
        loginForm.addEventListener('submit', function() {
            if (loginButton && loginSpinner) {
                loginButton.disabled = true;
                loginSpinner.classList.remove('d-none');
            }
        });
    }
    
    // Обработка Google-авторизации
    if (googleAuthForm && googleAuthButton) {
        console.log('Google auth form found');
        
        // Предотвращаем двойные клики
        googleAuthButton.addEventListener('click', function() {
            console.log('Google auth button clicked');
            
            // Показываем индикатор загрузки
            var spinnerHtml = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Переход к Google...';
            this.innerHTML = spinnerHtml;
            this.disabled = true;
            
            // Отправляем форму
            googleAuthForm.submit();
        });
    }
    
    // Отправка формы по нажатию Enter
    document.addEventListener('keydown', function(e) {
        if (e.keyCode === 13 || e.key === 'Enter') { // Enter key
            var activeElement = document.activeElement;
            if (activeElement.tagName !== 'TEXTAREA' && loginForm) {
                if (loginButton && loginSpinner) {
                    loginButton.disabled = true;
                    loginSpinner.classList.remove('d-none');
                }
                loginForm.submit();
                e.preventDefault();
            }
        }
    });
    
    // Проверяем и выводим куки для отладки
    console.log('Authentication cookies:');
    document.cookie.split(';').forEach(function(c) {
        var cookie = c.trim();
        if (cookie.includes('.AspNetCore.')) {
            console.log(cookie);
        }
    });
}); 