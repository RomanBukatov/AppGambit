// Простой Service Worker для AppGambit
const CACHE_NAME = 'appgambit-v1';

// Установка Service Worker
self.addEventListener('install', function(event) {
    console.log('Service Worker установлен');
    self.skipWaiting();
});

// Активация Service Worker
self.addEventListener('activate', function(event) {
    console.log('Service Worker активирован');
    event.waitUntil(self.clients.claim());
});

// Простая обработка запросов
self.addEventListener('fetch', function(event) {
    // Пропускаем все запросы без кэширования для избежания проблем
    return;
});