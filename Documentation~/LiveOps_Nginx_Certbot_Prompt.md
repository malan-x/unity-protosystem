# Nginx + Certbot — задание для Claude Code

## Контекст

Сервер: Debian, IP `91.149.242.72`  
Домен: `api.twohuyakproduction.com` (A-запись уже указывает на IP)  
PocketBase уже запущен на `127.0.0.1:8090`  
Дашборд лежит в `/opt/liveops/dashboard/`

## Задача

1. Установить nginx и certbot
2. Настроить nginx как reverse proxy для PocketBase
3. Получить SSL-сертификат через certbot
4. Проверить что всё работает

## Команды

```bash
# 1. Установка
apt update
apt install -y nginx certbot python3-certbot-nginx

# 2. Nginx конфиг
cat > /etc/nginx/sites-available/liveops.conf << 'EOF'
server {
    listen 80;
    server_name api.twohuyakproduction.com;

    location / {
        proxy_pass http://127.0.0.1:8090;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }

    location /dashboard/ {
        alias /opt/liveops/dashboard/;
        index index.html;
    }
}
EOF

# 3. Включить конфиг
ln -s /etc/nginx/sites-available/liveops.conf /etc/nginx/sites-enabled/
nginx -t && systemctl reload nginx

# 4. Получить SSL-сертификат
certbot --nginx -d api.twohuyakproduction.com --non-interactive --agree-tos -m admin@twohuyakproduction.com

# 5. Проверить автообновление
systemctl status certbot.timer
```

## Ожидаемый результат

- `https://api.twohuyakproduction.com` → PocketBase
- `https://api.twohuyakproduction.com/_/` → PocketBase Admin UI
- `https://api.twohuyakproduction.com/dashboard/` → дашборд
- HTTP автоматически редиректит на HTTPS (certbot добавит сам)

## Проверка

```bash
curl -I https://api.twohuyakproduction.com/api/health
# Ожидается: HTTP/2 200
```

## После успешной настройки

В Unity в `LiveOpsConfig.asset` изменить:
- `serverUrl`: `http://91.149.242.72:8090` → `https://api.twohuyakproduction.com`
