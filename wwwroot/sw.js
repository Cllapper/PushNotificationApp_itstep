self.addEventListener('push', event => {
  console.log('[Service Worker] Push Received.');

  const data = event.data.json();
  console.log('[Service Worker] Data:', data);

  const title = data.title;
  const options = {
      body: data.message
  };

  event.waitUntil(self.registration.showNotification(title, options));
});
