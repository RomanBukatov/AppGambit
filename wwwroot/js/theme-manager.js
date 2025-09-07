class ThemeManager {
    constructor() {
        this.currentTheme = 'light';
        this.storageKey = 'appgambit-theme';
        this.init();
    }

    async init() {
        // Загружаем сохраненную тему или определяем системную
        await this.loadTheme();
        
        // Создаем переключатель темы
        this.createThemeToggle();
        
        // Применяем тему
        this.applyTheme(this.currentTheme);
        
        // Слушаем изменения системной темы
        this.watchSystemTheme();
    }

    async loadTheme() {
        try {
            // Сначала пытаемся загрузить тему пользователя с сервера
            const response = await fetch('/api/theme/current');
            if (response.ok) {
                const data = await response.json();
                if (data.theme && data.source === 'user') {
                    this.currentTheme = data.theme === 'auto' ? this.getSystemTheme() : data.theme;
                    return;
                }
            }
        } catch (error) {
            console.log('Не удалось загрузить тему пользователя, используем локальную');
        }

        // Проверяем локально сохраненную тему
        const savedTheme = localStorage.getItem(this.storageKey);
        
        if (savedTheme) {
            this.currentTheme = savedTheme;
        } else {
            // Определяем системную тему
            this.currentTheme = this.getSystemTheme();
        }
    }

    getSystemTheme() {
        if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
            return 'dark';
        }
        return 'light';
    }

    watchSystemTheme() {
        if (window.matchMedia) {
            const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
            mediaQuery.addEventListener('change', (e) => {
                // Обновляем тему только если пользователь не выбрал тему вручную
                if (!localStorage.getItem(this.storageKey)) {
                    this.currentTheme = e.matches ? 'dark' : 'light';
                    this.applyTheme(this.currentTheme);
                    this.updateToggleState();
                }
            });
        }
    }

    createThemeToggle() {
        // Создаем переключатель для десктопа
        this.createDesktopToggle();
        
        // Создаем переключатель для мобильного меню
        this.createMobileToggle();
    }

    createDesktopToggle() {
        const navbar = document.querySelector('.navbar .container');
        if (!navbar) return;

        const themeToggle = document.createElement('div');
        themeToggle.className = 'theme-toggle d-none d-lg-flex me-3';
        themeToggle.innerHTML = `
            <i class="theme-toggle-icon sun fas fa-sun"></i>
            <div class="theme-toggle-switch"></div>
            <i class="theme-toggle-icon moon fas fa-moon"></i>
        `;

        // Вставляем перед поиском
        const searchForm = navbar.querySelector('form[asp-controller="Applications"]');
        if (searchForm) {
            navbar.insertBefore(themeToggle, searchForm);
        } else {
            navbar.appendChild(themeToggle);
        }

        // Добавляем обработчик
        themeToggle.addEventListener('click', () => {
            this.toggleTheme();
        });

        this.desktopToggle = themeToggle;
    }

    createMobileToggle() {
        // Добавляем в специальный контейнер вне collapse для мобильных
        const mobileContainer = document.querySelector('.theme-toggle-container');
        if (mobileContainer) {
            const mobileThemeToggle = document.createElement('div');
            mobileThemeToggle.className = 'theme-toggle';
            mobileThemeToggle.innerHTML = `
                <i class="theme-toggle-icon sun fas fa-sun"></i>
                <div class="theme-toggle-switch"></div>
                <i class="theme-toggle-icon moon fas fa-moon"></i>
            `;

            mobileContainer.appendChild(mobileThemeToggle);

            // Добавляем обработчик
            mobileThemeToggle.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                this.toggleTheme();
            });

            this.mobileToggle = mobileThemeToggle;
        }

        // Также добавляем в мобильное меню как дополнительную опцию
        const mobileMenu = document.querySelector('.navbar-collapse .navbar-nav');
        if (mobileMenu) {
            const mobileThemeItem = document.createElement('li');
            mobileThemeItem.className = 'nav-item d-lg-none';
            mobileThemeItem.innerHTML = `
                <div class="nav-link theme-toggle-mobile">
                    <i class="fas fa-palette me-2"></i>
                    <span>Переключить тему</span>
                </div>
            `;

            // Вставляем перед последним элементом (профиль)
            const lastItem = mobileMenu.lastElementChild;
            if (lastItem) {
                mobileMenu.insertBefore(mobileThemeItem, lastItem);
            } else {
                mobileMenu.appendChild(mobileThemeItem);
            }

            // Добавляем обработчик
            mobileThemeItem.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                this.toggleTheme();
            });
        }
    }

    async toggleTheme() {
        this.currentTheme = this.currentTheme === 'light' ? 'dark' : 'light';
        this.applyTheme(this.currentTheme);
        await this.saveTheme();
        this.updateToggleState();
        
        // Показываем уведомление
        this.showThemeNotification();
    }

    applyTheme(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        
        // Обновляем мета-тег для мобильных браузеров
        this.updateThemeColorMeta(theme);
        
        // Применяем тему к существующим элементам
        this.applyThemeToElements(theme);
    }

    updateThemeColorMeta(theme) {
        let metaThemeColor = document.querySelector('meta[name="theme-color"]');
        if (!metaThemeColor) {
            metaThemeColor = document.createElement('meta');
            metaThemeColor.name = 'theme-color';
            document.head.appendChild(metaThemeColor);
        }
        
        metaThemeColor.content = theme === 'dark' ? '#1a1a1a' : '#007bff';
    }

    applyThemeToElements(theme) {
        // Дополнительная обработка элементов, которые могут не покрываться CSS
        const isDark = theme === 'dark';
        
        // Обновляем favicon для темной темы (если есть темная версия)
        const favicon = document.querySelector('link[rel="icon"]');
        if (favicon && isDark) {
            // Можно добавить темную версию favicon
            // favicon.href = '/favicon-dark.ico';
        }
        
        // Обновляем цвет статус-бара для PWA
        const statusBarMeta = document.querySelector('meta[name="apple-mobile-web-app-status-bar-style"]');
        if (statusBarMeta) {
            statusBarMeta.content = isDark ? 'black-translucent' : 'default';
        }
    }

    updateToggleState() {
        const isDark = this.currentTheme === 'dark';
        
        // Обновляем состояние переключателей
        [this.desktopToggle, this.mobileToggle].forEach(toggle => {
            if (toggle) {
                const switchElement = toggle.querySelector('.theme-toggle-switch');
                if (switchElement) {
                    switchElement.classList.toggle('active', isDark);
                }
            }
        });
    }

    async saveTheme() {
        // Сохраняем локально
        localStorage.setItem(this.storageKey, this.currentTheme);
        
        // Сохраняем на сервере для авторизованных пользователей
        try {
            const response = await fetch('/api/theme/set', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': this.getAntiForgeryToken()
                },
                body: JSON.stringify({ theme: this.currentTheme })
            });
            
            if (response.ok) {
                console.log('Тема сохранена на сервере');
            }
        } catch (error) {
            console.log('Не удалось сохранить тему на сервере:', error);
        }
    }

    showThemeNotification() {
        const message = this.currentTheme === 'dark' ? 
            'Включена темная тема' : 
            'Включена светлая тема';
        
        const icon = this.currentTheme === 'dark' ? 'fa-moon' : 'fa-sun';
        
        // Создаем уведомление
        const notification = document.createElement('div');
        notification.className = 'theme-notification';
        notification.innerHTML = `
            <div class="theme-notification-content">
                <i class="fas ${icon} me-2"></i>
                ${message}
            </div>
        `;
        
        // Добавляем стили для уведомления
        notification.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            z-index: 9999;
            background: ${this.currentTheme === 'dark' ? '#2d2d2d' : '#ffffff'};
            color: ${this.currentTheme === 'dark' ? '#ffffff' : '#000000'};
            padding: 12px 20px;
            border-radius: 8px;
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
            border: 1px solid ${this.currentTheme === 'dark' ? '#404040' : '#e9ecef'};
            transform: translateX(100%);
            transition: transform 0.3s ease;
        `;
        
        document.body.appendChild(notification);
        
        // Анимация появления
        setTimeout(() => {
            notification.style.transform = 'translateX(0)';
        }, 100);
        
        // Удаляем через 3 секунды
        setTimeout(() => {
            notification.style.transform = 'translateX(100%)';
            setTimeout(() => {
                if (notification.parentNode) {
                    notification.parentNode.removeChild(notification);
                }
            }, 300);
        }, 3000);
    }

    // Публичные методы для внешнего использования
    getCurrentTheme() {
        return this.currentTheme;
    }

    async setTheme(theme) {
        if (theme === 'light' || theme === 'dark' || theme === 'auto') {
            this.currentTheme = theme === 'auto' ? this.getSystemTheme() : theme;
            this.applyTheme(this.currentTheme);
            await this.saveTheme();
            this.updateToggleState();
        }
    }

    async resetToSystemTheme() {
        localStorage.removeItem(this.storageKey);
        this.currentTheme = this.getSystemTheme();
        this.applyTheme(this.currentTheme);
        this.updateToggleState();
        this.showThemeNotification();
        
        // Сохраняем 'auto' на сервере
        try {
            await fetch('/api/theme/set', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': this.getAntiForgeryToken()
                },
                body: JSON.stringify({ theme: 'auto' })
            });
        } catch (error) {
            console.log('Не удалось сохранить автоматическую тему на сервере:', error);
        }
    }

    getAntiForgeryToken() {
        const token = document.querySelector('input[name="__RequestVerificationToken"]');
        return token ? token.value : '';
    }
}

// Глобальная переменная для доступа к менеджеру тем
let themeManager;

// Инициализация при загрузке DOM
document.addEventListener('DOMContentLoaded', () => {
    themeManager = new ThemeManager();
});

// Экспорт для использования в других скриптах
if (typeof module !== 'undefined' && module.exports) {
    module.exports = ThemeManager;
}