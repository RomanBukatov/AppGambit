class EnhancedSearch {
    constructor() {
        this.searchInput = null;
        this.searchContainer = null;
        this.suggestionsContainer = null;
        this.quickResultsContainer = null;
        this.searchTimeout = null;
        this.currentQuery = '';
        this.isVisible = false;
        this.selectedIndex = -1;
        this.suggestions = [];
        
        this.init();
    }

    init() {
        // Инициализация для десктопного поиска
        this.initDesktopSearch();
        
        // Инициализация для мобильного поиска
        this.initMobileSearch();
        
        // Обработчики событий
        this.bindEvents();
    }

    initDesktopSearch() {
        this.searchInput = document.querySelector('.search-input');
        if (!this.searchInput) return;

        this.searchContainer = this.searchInput.closest('.input-group');
        if (!this.searchContainer) return;

        // Создаем контейнер для предложений
        this.createSuggestionsContainer();
    }

    initMobileSearch() {
        const mobileSearchInput = document.querySelector('.mobile-search-form input');
        if (mobileSearchInput) {
            this.initSearchForInput(mobileSearchInput, 'mobile');
        }
    }

    createSuggestionsContainer() {
        // Создаем контейнер для предложений
        this.suggestionsContainer = document.createElement('div');
        this.suggestionsContainer.className = 'search-suggestions';
        this.suggestionsContainer.innerHTML = `
            <div class="suggestions-header">
                <div class="suggestions-tabs">
                    <button class="suggestions-tab active" data-tab="suggestions">
                        <i class="fas fa-search me-1"></i>Предложения
                    </button>
                    <button class="suggestions-tab" data-tab="results">
                        <i class="fas fa-list me-1"></i>Результаты
                    </button>
                </div>
                <button class="suggestions-close">
                    <i class="fas fa-times"></i>
                </button>
            </div>
            <div class="suggestions-content">
                <div class="suggestions-list" data-content="suggestions"></div>
                <div class="quick-results" data-content="results" style="display: none;"></div>
            </div>
            <div class="suggestions-footer">
                <div class="search-tips">
                    <small class="text-muted">
                        <i class="fas fa-lightbulb me-1"></i>
                        Используйте <kbd>↑</kbd> <kbd>↓</kbd> для навигации, <kbd>Enter</kbd> для выбора
                    </small>
                </div>
            </div>
        `;

        // Вставляем после input-group
        this.searchContainer.parentNode.insertBefore(this.suggestionsContainer, this.searchContainer.nextSibling);
        
        // Получаем ссылки на элементы
        this.suggestionsListContainer = this.suggestionsContainer.querySelector('[data-content="suggestions"]');
        this.quickResultsContainer = this.suggestionsContainer.querySelector('[data-content="results"]');
    }

    bindEvents() {
        if (!this.searchInput) return;

        // Основные события поиска
        this.searchInput.addEventListener('input', (e) => this.handleInput(e));
        this.searchInput.addEventListener('focus', (e) => this.handleFocus(e));
        this.searchInput.addEventListener('keydown', (e) => this.handleKeydown(e));
        
        // Клик вне области поиска
        document.addEventListener('click', (e) => this.handleOutsideClick(e));
        
        // События для табов
        if (this.suggestionsContainer) {
            this.suggestionsContainer.addEventListener('click', (e) => this.handleSuggestionsClick(e));
        }

        // Обработка мобильного поиска
        const mobileSearchBtn = document.getElementById('mobileSearchBtn');
        if (mobileSearchBtn) {
            mobileSearchBtn.addEventListener('click', (e) => {
                e.preventDefault();
                this.showMobileSearch();
            });
        }
    }

    handleInput(e) {
        const query = e.target.value.trim();
        this.currentQuery = query;

        // Очищаем предыдущий таймер
        if (this.searchTimeout) {
            clearTimeout(this.searchTimeout);
        }

        if (query.length < 2) {
            this.hideSuggestions();
            return;
        }

        // Задержка для избежания слишком частых запросов
        this.searchTimeout = setTimeout(() => {
            this.fetchSuggestions(query);
        }, 300);
    }

    handleFocus(e) {
        if (this.currentQuery.length >= 2) {
            this.showSuggestions();
        }
    }

    handleKeydown(e) {
        if (!this.isVisible) return;

        switch (e.key) {
            case 'ArrowDown':
                e.preventDefault();
                this.navigateDown();
                break;
            case 'ArrowUp':
                e.preventDefault();
                this.navigateUp();
                break;
            case 'Enter':
                e.preventDefault();
                this.selectCurrent();
                break;
            case 'Escape':
                e.preventDefault();
                this.hideSuggestions();
                this.searchInput.blur();
                break;
        }
    }

    handleOutsideClick(e) {
        if (!this.suggestionsContainer) return;
        
        if (!this.searchContainer.contains(e.target) && 
            !this.suggestionsContainer.contains(e.target)) {
            this.hideSuggestions();
        }
    }

    handleSuggestionsClick(e) {
        // Обработка кликов по табам
        if (e.target.closest('.suggestions-tab')) {
            const tab = e.target.closest('.suggestions-tab');
            const tabType = tab.dataset.tab;
            this.switchTab(tabType);
            return;
        }

        // Обработка кнопки закрытия
        if (e.target.closest('.suggestions-close')) {
            this.hideSuggestions();
            return;
        }

        // Обработка кликов по предложениям
        if (e.target.closest('.suggestion-item')) {
            const item = e.target.closest('.suggestion-item');
            const url = item.dataset.url;
            if (url) {
                window.location.href = url;
            }
            return;
        }
    }

    async fetchSuggestions(query) {
        try {
            const response = await fetch(`/api/search/suggestions?query=${encodeURIComponent(query)}&limit=10`);
            const data = await response.json();
            
            this.suggestions = data.suggestions || [];
            this.renderSuggestions();
            this.showSuggestions();
        } catch (error) {
            console.error('Ошибка получения предложений:', error);
        }
    }

    async fetchQuickResults(query) {
        try {
            const response = await fetch(`/api/search/quick?query=${encodeURIComponent(query)}&limit=20`);
            const data = await response.json();
            
            this.renderQuickResults(data.results || [], data.total || 0);
        } catch (error) {
            console.error('Ошибка получения быстрых результатов:', error);
        }
    }

    renderSuggestions() {
        if (!this.suggestionsListContainer) return;

        if (this.suggestions.length === 0) {
            this.suggestionsListContainer.innerHTML = `
                <div class="no-suggestions">
                    <i class="fas fa-search text-muted mb-2"></i>
                    <p class="text-muted mb-0">Предложения не найдены</p>
                </div>
            `;
            return;
        }

        const groupedSuggestions = this.groupSuggestions(this.suggestions);
        let html = '';

        Object.keys(groupedSuggestions).forEach(type => {
            const items = groupedSuggestions[type];
            if (items.length === 0) return;

            const typeInfo = this.getTypeInfo(type);
            html += `
                <div class="suggestion-group">
                    <div class="suggestion-group-header">
                        <i class="${typeInfo.icon} me-2"></i>
                        ${typeInfo.title}
                    </div>
                    <div class="suggestion-group-items">
                        ${items.map(item => this.renderSuggestionItem(item)).join('')}
                    </div>
                </div>
            `;
        });

        this.suggestionsListContainer.innerHTML = html;
    }

    renderQuickResults(results, total) {
        if (!this.quickResultsContainer) return;

        if (results.length === 0) {
            this.quickResultsContainer.innerHTML = `
                <div class="no-results">
                    <i class="fas fa-search text-muted mb-2"></i>
                    <p class="text-muted mb-0">Результаты не найдены</p>
                </div>
            `;
            return;
        }

        let html = `
            <div class="quick-results-header">
                <span class="results-count">Найдено: ${total} приложений</span>
                <a href="/Applications?search=${encodeURIComponent(this.currentQuery)}" class="view-all-link">
                    Показать все <i class="fas fa-arrow-right ms-1"></i>
                </a>
            </div>
            <div class="quick-results-list">
        `;

        results.forEach(app => {
            html += `
                <div class="quick-result-item" data-url="/Applications/Details/${app.id}">
                    <div class="result-icon">
                        ${app.iconUrl ? 
                            `<img src="${app.iconUrl}" alt="${app.name}" class="app-icon">` :
                            `<div class="app-icon-placeholder"><i class="fas fa-desktop"></i></div>`
                        }
                    </div>
                    <div class="result-content">
                        <div class="result-title">${app.name}</div>
                        <div class="result-description">${app.description}</div>
                        <div class="result-meta">
                            <span class="result-author">
                                <i class="fas fa-user me-1"></i>${app.userDisplayName}
                            </span>
                            <span class="result-downloads">
                                <i class="fas fa-download me-1"></i>${app.downloadCount}
                            </span>
                            ${app.category ? `<span class="result-category badge bg-primary">${app.category}</span>` : ''}
                        </div>
                    </div>
                    <div class="result-rating">
                        <div class="rating-stars">
                            ${this.renderStars(app.averageRating)}
                        </div>
                        <small class="text-muted">(${app.totalRatings})</small>
                    </div>
                </div>
            `;
        });

        html += '</div>';
        this.quickResultsContainer.innerHTML = html;
    }

    renderSuggestionItem(item) {
        return `
            <div class="suggestion-item" data-url="${item.url}">
                <div class="suggestion-icon">
                    ${item.iconUrl ? 
                        `<img src="${item.iconUrl}" alt="${item.title}" class="suggestion-img">` :
                        `<div class="suggestion-placeholder"><i class="fas fa-${this.getTypeIcon(item.type)}"></i></div>`
                    }
                </div>
                <div class="suggestion-content">
                    <div class="suggestion-title">${item.title}</div>
                    <div class="suggestion-description">${item.description}</div>
                </div>
                ${item.type === 'application' ? `
                    <div class="suggestion-meta">
                        <span class="download-count">
                            <i class="fas fa-download me-1"></i>${item.downloadCount}
                        </span>
                    </div>
                ` : ''}
            </div>
        `;
    }

    groupSuggestions(suggestions) {
        return suggestions.reduce((groups, item) => {
            const type = item.type;
            if (!groups[type]) {
                groups[type] = [];
            }
            groups[type].push(item);
            return groups;
        }, {});
    }

    getTypeInfo(type) {
        const types = {
            application: { title: 'Приложения', icon: 'fas fa-mobile-alt' },
            user: { title: 'Пользователи', icon: 'fas fa-user' },
            category: { title: 'Категории', icon: 'fas fa-folder' }
        };
        return types[type] || { title: 'Другое', icon: 'fas fa-question' };
    }

    getTypeIcon(type) {
        const icons = {
            application: 'mobile-alt',
            user: 'user',
            category: 'folder'
        };
        return icons[type] || 'question';
    }

    renderStars(rating) {
        let stars = '';
        for (let i = 1; i <= 5; i++) {
            if (i <= rating) {
                stars += '<i class="fas fa-star text-warning"></i>';
            } else {
                stars += '<i class="far fa-star text-muted"></i>';
            }
        }
        return stars;
    }

    switchTab(tabType) {
        // Обновляем активный таб
        this.suggestionsContainer.querySelectorAll('.suggestions-tab').forEach(tab => {
            tab.classList.remove('active');
        });
        this.suggestionsContainer.querySelector(`[data-tab="${tabType}"]`).classList.add('active');

        // Показываем соответствующий контент
        this.suggestionsContainer.querySelectorAll('[data-content]').forEach(content => {
            content.style.display = 'none';
        });
        this.suggestionsContainer.querySelector(`[data-content="${tabType}"]`).style.display = 'block';

        // Загружаем данные для результатов, если нужно
        if (tabType === 'results' && this.currentQuery.length >= 2) {
            this.fetchQuickResults(this.currentQuery);
        }
    }

    navigateDown() {
        const items = this.suggestionsContainer.querySelectorAll('.suggestion-item, .quick-result-item');
        if (items.length === 0) return;

        this.selectedIndex = Math.min(this.selectedIndex + 1, items.length - 1);
        this.updateSelection(items);
    }

    navigateUp() {
        const items = this.suggestionsContainer.querySelectorAll('.suggestion-item, .quick-result-item');
        if (items.length === 0) return;

        this.selectedIndex = Math.max(this.selectedIndex - 1, -1);
        this.updateSelection(items);
    }

    updateSelection(items) {
        items.forEach((item, index) => {
            item.classList.toggle('selected', index === this.selectedIndex);
        });
    }

    selectCurrent() {
        const items = this.suggestionsContainer.querySelectorAll('.suggestion-item, .quick-result-item');
        if (this.selectedIndex >= 0 && this.selectedIndex < items.length) {
            const selectedItem = items[this.selectedIndex];
            const url = selectedItem.dataset.url;
            if (url) {
                window.location.href = url;
            }
        } else {
            // Если ничего не выбрано, выполняем обычный поиск
            this.performSearch();
        }
    }

    performSearch() {
        if (this.currentQuery.trim()) {
            window.location.href = `/Applications?search=${encodeURIComponent(this.currentQuery.trim())}`;
        }
    }

    showSuggestions() {
        if (!this.suggestionsContainer) return;
        
        this.suggestionsContainer.style.display = 'block';
        this.isVisible = true;
        this.selectedIndex = -1;
    }

    hideSuggestions() {
        if (!this.suggestionsContainer) return;
        
        this.suggestionsContainer.style.display = 'none';
        this.isVisible = false;
        this.selectedIndex = -1;
    }

    showMobileSearch() {
        // Создаем мобильное окно поиска
        const mobileSearchModal = document.createElement('div');
        mobileSearchModal.className = 'mobile-search-modal';
        mobileSearchModal.innerHTML = `
            <div class="mobile-search-content">
                <div class="mobile-search-header">
                    <div class="mobile-search-input-group">
                        <input type="text" class="form-control mobile-search-input" placeholder="🔍 Поиск приложений..." autofocus>
                        <button class="btn btn-outline-secondary mobile-search-close">
                            <i class="fas fa-times"></i>
                        </button>
                    </div>
                </div>
                <div class="mobile-search-results"></div>
            </div>
        `;

        document.body.appendChild(mobileSearchModal);
        
        // Инициализируем поиск для мобильного окна
        const mobileInput = mobileSearchModal.querySelector('.mobile-search-input');
        const closeBtn = mobileSearchModal.querySelector('.mobile-search-close');
        const resultsContainer = mobileSearchModal.querySelector('.mobile-search-results');

        // События
        closeBtn.addEventListener('click', () => {
            document.body.removeChild(mobileSearchModal);
        });

        mobileInput.addEventListener('input', (e) => {
            const query = e.target.value.trim();
            if (query.length >= 2) {
                this.fetchMobileResults(query, resultsContainer);
            } else {
                resultsContainer.innerHTML = '';
            }
        });

        mobileInput.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                const query = e.target.value.trim();
                if (query) {
                    window.location.href = `/Applications?search=${encodeURIComponent(query)}`;
                }
            }
        });
    }

    async fetchMobileResults(query, container) {
        try {
            const response = await fetch(`/api/search/quick?query=${encodeURIComponent(query)}&limit=10`);
            const data = await response.json();
            
            this.renderMobileResults(data.results || [], container);
        } catch (error) {
            console.error('Ошибка мобильного поиска:', error);
        }
    }

    renderMobileResults(results, container) {
        if (results.length === 0) {
            container.innerHTML = `
                <div class="mobile-no-results">
                    <i class="fas fa-search text-muted mb-2"></i>
                    <p class="text-muted">Результаты не найдены</p>
                </div>
            `;
            return;
        }

        let html = '<div class="mobile-results-list">';
        results.forEach(app => {
            html += `
                <a href="/Applications/Details/${app.id}" class="mobile-result-item">
                    <div class="mobile-result-icon">
                        ${app.iconUrl ? 
                            `<img src="${app.iconUrl}" alt="${app.name}">` :
                            `<div class="mobile-icon-placeholder"><i class="fas fa-desktop"></i></div>`
                        }
                    </div>
                    <div class="mobile-result-content">
                        <div class="mobile-result-title">${app.name}</div>
                        <div class="mobile-result-meta">
                            <span><i class="fas fa-user me-1"></i>${app.userDisplayName}</span>
                            <span><i class="fas fa-download me-1"></i>${app.downloadCount}</span>
                        </div>
                    </div>
                </a>
            `;
        });
        html += '</div>';
        
        container.innerHTML = html;
    }
}

// Инициализация при загрузке страницы
document.addEventListener('DOMContentLoaded', () => {
    new EnhancedSearch();
});