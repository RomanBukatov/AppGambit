// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Простое решение для ускорения навигации без полной перезагрузки страницы
document.addEventListener('DOMContentLoaded', function() {
    // Кэш для хранения загруженных страниц
    const pageCache = {};
    
    // Флаг для отслеживания текущей загрузки
    let isNavigating = false;
    
    // Создаем блок для отображения прогресса загрузки
    const progressBar = document.createElement('div');
    progressBar.className = 'navigation-progress';
    progressBar.style.cssText = `
        position: fixed;
        top: 0;
        left: 0;
        height: 3px;
        background-color: var(--primary-color, #0d6efd);
        z-index: 9999;
        width: 0;
        transition: width 0.3s ease-out, opacity 0.3s ease-out;
        opacity: 0;
    `;
    document.body.appendChild(progressBar);
    
    // Функция для обновления прогресс-бара
    function updateProgress(percent) {
        progressBar.style.opacity = '1';
        progressBar.style.width = percent + '%';
        if (percent >= 100) {
            setTimeout(() => {
                progressBar.style.opacity = '0';
                setTimeout(() => {
                    progressBar.style.width = '0';
                }, 300);
            }, 200);
        }
    }
    
    // Обработчик кликов по ссылкам
    document.addEventListener('click', function(e) {
        // Проверяем, что это клик по ссылке внутри сайта
        const link = e.target.closest('a');
        if (!link || 
            !link.href || 
            link.target === '_blank' || 
            link.hasAttribute('download') || 
            link.href.startsWith('javascript:') ||
            link.href.includes('#') || 
            link.getAttribute('data-turbo') === 'false' ||
            !link.href.startsWith(window.location.origin)) {
            return;
        }
        
        // Не перехватываем клики по внешним ссылкам или с модификаторами
        if (e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) {
            return;
        }
        
        // Если форма поблизости, не перехватываем
        if (link.closest('form')) {
            return;
        }
        
        // Предотвращаем стандартный переход
        e.preventDefault();
        
        if (isNavigating) return;
        isNavigating = true;
        
        const targetUrl = link.href;
        
        // Показываем прогресс
        updateProgress(20);
        
        // Проверяем кэш
        if (pageCache[targetUrl]) {
            updateProgress(70);
            updatePage(pageCache[targetUrl], targetUrl);
            return;
        }
        
        // Загружаем страницу
        fetch(targetUrl, {
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            }
        })
        .then(response => {
            updateProgress(50);
            if (!response.ok) {
                throw new Error('Network response was not ok');
            }
            return response.text();
        })
        .then(html => {
            updateProgress(80);
            // Кэшируем страницу
            pageCache[targetUrl] = html;
            // Обновляем содержимое
            updatePage(html, targetUrl);
        })
        .catch(error => {
            console.error('Ошибка при загрузке страницы:', error);
            // В случае ошибки просто переходим традиционным способом
            window.location.href = targetUrl;
        });
    });
    
    // Функция для обновления содержимого страницы
    function updatePage(html, targetUrl) {
        // Создаем временный элемент для разбора HTML
        const doc = new DOMParser().parseFromString(html, 'text/html');
        
        // Извлекаем нужные элементы из загруженной страницы
        const newTitle = doc.querySelector('title').textContent;
        const newMain = doc.querySelector('main').innerHTML;
        
        // Обновляем содержимое текущей страницы
        document.querySelector('main').innerHTML = newMain;
        document.title = newTitle;
        
        // Обновляем URL в адресной строке
        window.history.pushState({}, newTitle, targetUrl);
        
        // Прокручиваем страницу вверх
        window.scrollTo(0, 0);
        
        // Запускаем скрипты на новой странице
        runScripts();
        
        // Завершаем загрузку
        updateProgress(100);
        isNavigating = false;
        
        // Отправляем событие, что страница загружена
        window.dispatchEvent(new CustomEvent('turbo:load'));
    }
    
    // Функция для запуска скриптов на новой странице
    function runScripts() {
        // Здесь можно инициализировать компоненты, которые
        // должны работать на каждой странице
    }
    
    // Обработка навигации по кнопкам браузера
    window.addEventListener('popstate', function(e) {
        // Если в истории есть закэшированная страница - используем ее
        if (pageCache[window.location.href]) {
            updatePage(pageCache[window.location.href], window.location.href);
        } else {
            // Иначе просто перезагружаем страницу
            window.location.reload();
        }
    });
});
