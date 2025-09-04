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
        // Пропускаем формы комментариев, которые обрабатываются через AJAX
        if (form.id === 'comment-form' ||
            form.closest('.comment-edit-form') ||
            form.hasAttribute('data-ajax-form')) {
            console.log('🚫 Пропускаем AJAX форму:', form.id || form.className);
            return;
        }
        
        console.log('✅ Добавляем обработчик для обычной формы:', form.id || form.className);
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
    
    // Мобильные улучшения
    initMobileEnhancements();
    
    // Инициализация улучшенной мобильной навигации
    initImprovedMobileNavigation();
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

// Мобильные улучшения
function initMobileEnhancements() {
    // Определение мобильного устройства
    const isMobile = /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);
    
    if (isMobile) {
        document.body.classList.add('is-mobile');
    }
    
    // Улучшение тач-событий
    if ('ontouchstart' in window) {
        document.body.classList.add('touch-device');
        
        // Убираем задержку клика на мобильных
        const elements = document.querySelectorAll('a, button, .btn');
        elements.forEach(el => {
            el.addEventListener('touchstart', function() {
                this.classList.add('touch-active');
            });
            
            el.addEventListener('touchend', function() {
                setTimeout(() => {
                    this.classList.remove('touch-active');
                }, 100);
            });
        });
    }
    
    // Фикс для viewport height на iOS
    function setViewportHeight() {
        const vh = window.innerHeight * 0.01;
        document.documentElement.style.setProperty('--vh', `${vh}px`);
    }
    
    setViewportHeight();
    window.addEventListener('resize', setViewportHeight);
    window.addEventListener('orientationchange', setViewportHeight);
    
    // Улучшение скролла для мобильных
    if (isMobile) {
        // Плавный скролл для якорных ссылок
        document.querySelectorAll('a[href^="#"]').forEach(anchor => {
            anchor.addEventListener('click', function (e) {
                e.preventDefault();
                const target = document.querySelector(this.getAttribute('href'));
                if (target) {
                    target.scrollIntoView({
                        behavior: 'smooth',
                        block: 'start'
                    });
                }
            });
        });
        
        // Скрытие адресной строки при скролле
        let lastScrollTop = 0;
        const navbar = document.querySelector('.navbar');
        
        window.addEventListener('scroll', function() {
            const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
            
            if (scrollTop > lastScrollTop && scrollTop > 100) {
                // Скролл вниз
                navbar.style.transform = 'translateY(-100%)';
            } else {
                // Скролл вверх
                navbar.style.transform = 'translateY(0)';
            }
            
            lastScrollTop = scrollTop <= 0 ? 0 : scrollTop;
        }, { passive: true });
    }
    
    // Оптимизация изображений для мобильных
    if (isMobile && 'connection' in navigator) {
        const connection = navigator.connection || navigator.mozConnection || navigator.webkitConnection;
        
        if (connection && connection.effectiveType) {
            // Загружаем изображения меньшего размера на медленном соединении
            if (connection.effectiveType === '2g' || connection.effectiveType === 'slow-2g') {
                document.querySelectorAll('img[data-mobile-src]').forEach(img => {
                    img.src = img.getAttribute('data-mobile-src');
                });
            }
        }
    }
    
    // Улучшение форм для мобильных
    if (isMobile) {
        // Автоматическое увеличение текстовых полей при фокусе
        document.querySelectorAll('textarea').forEach(textarea => {
            textarea.addEventListener('focus', function() {
                this.style.minHeight = '120px';
            });
            
            textarea.addEventListener('blur', function() {
                if (!this.value.trim()) {
                    this.style.minHeight = '';
                }
            });
        });
        
        // Показ/скрытие пароля
        document.querySelectorAll('input[type="password"]').forEach(input => {
            const wrapper = document.createElement('div');
            wrapper.className = 'password-wrapper position-relative';
            input.parentNode.insertBefore(wrapper, input);
            wrapper.appendChild(input);
            
            const toggleBtn = document.createElement('button');
            toggleBtn.type = 'button';
            toggleBtn.className = 'btn btn-sm position-absolute end-0 top-50 translate-middle-y me-2';
            toggleBtn.innerHTML = '<i class="fas fa-eye"></i>';
            toggleBtn.style.zIndex = '10';
            
            toggleBtn.addEventListener('click', function() {
                if (input.type === 'password') {
                    input.type = 'text';
                    this.innerHTML = '<i class="fas fa-eye-slash"></i>';
                } else {
                    input.type = 'password';
                    this.innerHTML = '<i class="fas fa-eye"></i>';
                }
            });
            
            wrapper.appendChild(toggleBtn);
        });
    }
}

// Функция для инициализации улучшенной мобильной навигации
function initImprovedMobileNavigation() {
    // Управление боковым меню
    const navbarToggler = document.querySelector('.navbar-toggler');
    const navbarCollapse = document.querySelector('.navbar-collapse');
    const mobileMenuOverlay = document.getElementById('mobileMenuOverlay');
    const mobileMenuClose = document.getElementById('mobileMenuClose');
    
    // Работаем только на мобильных устройствах
    if (window.innerWidth <= 768) {
        if (navbarToggler && navbarCollapse && mobileMenuOverlay) {
            // Отключаем стандартное поведение Bootstrap
            navbarToggler.removeAttribute('data-bs-toggle');
            navbarToggler.removeAttribute('data-bs-target');
            
            // Открытие меню
            navbarToggler.addEventListener('click', function(e) {
                e.preventDefault();
                e.stopPropagation();
                navbarCollapse.classList.add('show');
                mobileMenuOverlay.classList.add('show');
                document.body.style.overflow = 'hidden';
            });
        
        // Закрытие меню при клике на оверлей
        mobileMenuOverlay.addEventListener('click', function() {
            navbarCollapse.classList.remove('show');
            mobileMenuOverlay.classList.remove('show');
            document.body.style.overflow = '';
        });
        
        // Закрытие меню при клике на кнопку закрытия
        if (mobileMenuClose) {
            mobileMenuClose.addEventListener('click', function() {
                navbarCollapse.classList.remove('show');
                mobileMenuOverlay.classList.remove('show');
                document.body.style.overflow = '';
            });
        }
        
        // Закрытие меню при клике на ссылку
        const navLinks = navbarCollapse.querySelectorAll('.nav-link');
        navLinks.forEach(link => {
            link.addEventListener('click', function() {
                if (window.innerWidth <= 576) {
                    navbarCollapse.classList.remove('show');
                    mobileMenuOverlay.classList.remove('show');
                    document.body.style.overflow = '';
                }
            });
        });
        }
    }
    
    // Поиск через нижнюю панель
    const mobileSearchBtn = document.getElementById('mobileSearchBtn');
    if (mobileSearchBtn) {
        mobileSearchBtn.addEventListener('click', function(e) {
            e.preventDefault();
            // Создаем модальное окно для поиска
            const searchModal = document.createElement('div');
            searchModal.className = 'mobile-search-modal';
            searchModal.innerHTML = `
                <div class="mobile-search-modal-content">
                    <div class="mobile-search-modal-header">
                        <h5>Поиск приложений</h5>
                        <button type="button" class="mobile-search-modal-close">
                            <i class="fas fa-times"></i>
                        </button>
                    </div>
                    <form action="/Applications" method="get">
                        <div class="input-group">
                            <input type="search" name="search" class="form-control" placeholder="Введите название..." autofocus>
                            <button class="btn btn-primary" type="submit">
                                <i class="fas fa-search"></i>
                            </button>
                        </div>
                    </form>
                </div>
            `;
            
            document.body.appendChild(searchModal);
            
            // Фокус на поле ввода
            setTimeout(() => {
                searchModal.querySelector('input').focus();
            }, 100);
            
            // Закрытие модального окна
            searchModal.querySelector('.mobile-search-modal-close').addEventListener('click', function() {
                searchModal.remove();
            });
            
            // Закрытие при клике вне окна
            searchModal.addEventListener('click', function(e) {
                if (e.target === searchModal) {
                    searchModal.remove();
                }
            });
        });
    }
    
    // Добавляем стили для модального окна поиска
    if (!document.getElementById('mobileSearchModalStyles')) {
        const style = document.createElement('style');
        style.id = 'mobileSearchModalStyles';
        style.textContent = `
            .mobile-search-modal {
                position: fixed;
                top: 0;
                left: 0;
                width: 100%;
                height: 100%;
                background: rgba(0, 0, 0, 0.5);
                z-index: 1060;
                display: flex;
                align-items: flex-start;
                padding-top: 20%;
            }
            
            .mobile-search-modal-content {
                background: white;
                width: 90%;
                max-width: 500px;
                margin: 0 auto;
                padding: 1.5rem;
                border-radius: 0.5rem;
                box-shadow: 0 4px 20px rgba(0, 0, 0, 0.2);
            }
            
            .mobile-search-modal-header {
                display: flex;
                justify-content: space-between;
                align-items: center;
                margin-bottom: 1rem;
            }
            
            .mobile-search-modal-header h5 {
                margin: 0;
                font-size: 1.25rem;
            }
            
            .mobile-search-modal-close {
                background: none;
                border: none;
                font-size: 1.5rem;
                color: #6c757d;
                cursor: pointer;
            }
            
            .mobile-search-modal .form-control {
                font-size: 16px;
            }
        `;
        document.head.appendChild(style);
    }
    
    // Модальное окно профиля для мобильных
    const mobileProfileBtn = document.getElementById('mobileProfileBtn');
    const mobileProfileModal = document.getElementById('mobileProfileModal');
    const mobileProfileOverlay = document.getElementById('mobileProfileOverlay');
    const mobileProfileClose = document.getElementById('mobileProfileClose');
    
    if (mobileProfileBtn && mobileProfileModal && mobileProfileOverlay) {
        // Открытие модального окна профиля
        mobileProfileBtn.addEventListener('click', function(e) {
            e.preventDefault();
            mobileProfileModal.classList.add('show');
            mobileProfileOverlay.classList.add('show');
            document.body.style.overflow = 'hidden';
        });
        
        // Закрытие модального окна
        function closeMobileProfileModal() {
            mobileProfileModal.classList.remove('show');
            mobileProfileOverlay.classList.remove('show');
            document.body.style.overflow = '';
        }
        
        // Закрытие по кнопке
        if (mobileProfileClose) {
            mobileProfileClose.addEventListener('click', closeMobileProfileModal);
        }
        
        // Закрытие по клику на оверлей
        mobileProfileOverlay.addEventListener('click', closeMobileProfileModal);
        
        // Закрытие свайпом вниз
        let startY = 0;
        let currentY = 0;
        let isDragging = false;
        
        mobileProfileModal.addEventListener('touchstart', function(e) {
            startY = e.touches[0].clientY;
            isDragging = true;
        });
        
        mobileProfileModal.addEventListener('touchmove', function(e) {
            if (!isDragging) return;
            
            currentY = e.touches[0].clientY;
            const deltaY = currentY - startY;
            
            if (deltaY > 0) {
                mobileProfileModal.style.transform = `translateY(${deltaY}px)`;
            }
        });
        
        mobileProfileModal.addEventListener('touchend', function(e) {
            if (!isDragging) return;
            
            const deltaY = currentY - startY;
            
            if (deltaY > 100) {
                closeMobileProfileModal();
            }
            
            mobileProfileModal.style.transform = '';
            isDragging = false;
        });
    }
}

// Экспорт функций для использования в других скриптах
window.AppGambitOptimizations = {
    initLazyLoading,
    preloadCriticalResources,
    initMobileEnhancements,
    initImprovedMobileNavigation
};
