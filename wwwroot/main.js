const log = (message) => document.getElementById('log').innerText = message;

const registerBtn = document.getElementById('registerBtn');
const subscribeBtn = document.getElementById('subscribeBtn');
const sendToMeBtn = document.getElementById('sendToMeBtn');
const broadcastBtn = document.getElementById('broadcastBtn');

let currentUserId = null;
let serviceWorkerRegistration = null;

async function registerServiceWorker() {
    if ('serviceWorker' in navigator) {
        try {
            serviceWorkerRegistration = await navigator.serviceWorker.register('/sw.js');
            console.log('Service Worker Registered');
        } catch (error) {
            console.error('Service Worker registration failed:', error);
        }
    }
}

function urlBase64ToUint8Array(base64String) {
    const padding = '='.repeat((4 - base64String.length % 4) % 4);
    const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
    const rawData = window.atob(base64);
    return Uint8Array.from([...rawData].map((char) => char.charCodeAt(0)));
}

registerBtn.addEventListener('click', async () => {
    const name = document.getElementById('name').value;
    const email = document.getElementById('email').value;

    if (!name || !email) {
        log('Введіть ім\'я та email');
        return;
    }

    const response = await fetch('/api/users', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, email }),
    });

    if (response.ok) {
        const user = await response.json();
        currentUserId = user.id;
        log(`Користувач ${user.name} успішно створений з ID: ${user.id}`);

        document.getElementById('registration-form').classList.add('d-none');
        document.getElementById('userInfo').classList.remove('d-none');
        document.getElementById('userName').innerText = user.name;
        document.getElementById('userId').innerText = user.id;
        subscribeBtn.disabled = false;
        sendToMeBtn.disabled = false;
    } else {
        log('Помилка реєстрації.');
    }
});

subscribeBtn.addEventListener('click', async () => {
    const permission = await Notification.requestPermission();
    if (permission !== 'granted') {
        log('Дозвіл на сповіщення не надано.');
        return;
    }

    const keyResponse = await fetch('/api/push/key');
    const publicKey = await keyResponse.text();

    const subscription = await serviceWorkerRegistration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: urlBase64ToUint8Array(publicKey),
    });

    const subJson = subscription.toJSON();

    const subscribeResponse = await fetch('/api/push/subscribe', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            userId: currentUserId,
            endpoint: subJson.endpoint,
            p256dh: subJson.keys.p256dh,
            auth: subJson.keys.auth,
        }),
    });

    if (subscribeResponse.ok) {
        log('Ви успішно підписалися на сповіщення!');
    } else {
        log('Помилка підписки.');
    }
});

sendToMeBtn.addEventListener('click', async () => {
     const response = await fetch(`/api/push/send/${currentUserId}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            title: 'Тест для вас!',
            message: `Це персональне сповіщення для користувача ID ${currentUserId}.`
        }),
    });
    log(await response.text());
});

broadcastBtn.addEventListener('click', async () => {
     const response = await fetch('/api/push/broadcast', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            title: 'Загальне оголошення',
            message: 'Це сповіщення для всіх підписаних користувачів.'
        }),
    });
    log(await response.text());
});


registerServiceWorker();
