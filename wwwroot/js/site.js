// Простой JavaScript для AppGambit
document.addEventListener('DOMContentLoaded', function() {
    // Скрываем прелоадер
    const loader = document.querySelector('.page-loader');
    if (loader) {
        setTimeout(() => {
            loader.classList.add('hidden');
            setTimeout(() => loader.remove(), 300);
        }, 500);
    }

    // Простая оптимизация форм
    const forms = document.querySelectorAll('form');
    forms.forEach(form => {
        form.addEventListener('submit', function() {
            const submitBtn = form.querySelector('button[type="submit"], input[type="submit"]');
            if (submitBtn && !submitBtn.disabled) {
                submitBtn.disabled = true;
                const originalText = submitBtn.innerHTML;
                submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Загрузка...';
                
                // Восстанавливаем кнопку через 5 секунд на случай ошибки
                setTimeout(() => {
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = originalText;
                }, 5000);
            }
        });
    });
    // Инициализация ленивой загрузки изображений
    initLazyLoading();
    
    // Предзагрузка критических ресурсов
    preloadCriticalResources();
});

// Ленивая загрузка изображений
function initLazyLoading() {
    const lazyImages = document.querySelectorAll('.lazy-load');
    
    if ('IntersectionObserver' in window) {
        const imageObserver = new IntersectionObserver((entries, observer) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const img = entry.target;
                    const src = img.getAttribute('data-src');
                    
                    if (src) {
                        img.src = src;
                        img.classList.add('loaded');
                        img.removeAttribute('data-src');
                        observer.unobserve(img);
                    }
                }
            });
        }, {
            rootMargin: '50px 0px',
            threshold: 0.01
        });
        
        lazyImages.forEach(img => {
            imageObserver.observe(img);
        });
    } else {
        // Fallback для старых браузеров
        lazyImages.forEach(img => {
            const src = img.getAttribute('data-src');
            if (src) {
                img.src = src;
                img.classList.add('loaded');
                img.removeAttribute('data-src');
            }
        });
    }
}

// Предзагрузка критических ресурсов
function preloadCriticalResources() {
    const criticalImages = document.querySelectorAll('img[src*="icon"]:not(.lazy-load)');
    criticalImages.forEach(img => {
        const link = document.createElement('link');
        link.rel = 'preload';
        link.as = 'image';
        link.href = img.src;
        document.head.appendChild(link);
    });
}

// Простая функция для удаления неиспользуемых стилей
window.addEventListener('load', function() {
    setTimeout(() => {
        const unusedStyles = document.querySelectorAll('style[data-remove-after-load]');
        unusedStyles.forEach(style => style.remove());
    }, 1000);
});

// Экспорт функций для использования в других скриптах
window.AppGambitOptimizations = {
    initLazyLoading,
    preloadCriticalResources
};
