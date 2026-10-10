// Pin the same verified SDK version for all imports; do not use "latest".
import { initializeApp } from "https://www.gstatic.com/firebasejs/12.19.0/firebase-app.js";
import { getAuth, signInAnonymously } from "https://www.gstatic.com/firebasejs/12.19.0/firebase-auth.js";
import { getDatabase, ref, set, serverTimestamp } from "https://www.gstatic.com/firebasejs/12.19.0/firebase-database.js";

// Replace this placeholder with the public WEB APP configuration from your
// Firebase project. Never put a service-account private key in a Web build.
const firebaseConfig = {
  apiKey: "REPLACE_WITH_WEB_CONFIG",
  authDomain: "REPLACE_WITH_WEB_CONFIG",
  databaseURL: "REPLACE_WITH_EXACT_DATABASE_URL",
  projectId: "REPLACE_WITH_WEB_CONFIG",
  appId: "REPLACE_WITH_WEB_CONFIG"
};
const app = initializeApp(firebaseConfig);
const auth = getAuth(app);
const db = getDatabase(app);
let flushing = false;

window.cbcFlush = async function () {
  if (flushing || !navigator.onLine) return;
  flushing = true;
  try {
    await auth.authStateReady();
    if (!auth.currentUser) await signInAnonymously(auth);
    while (window.cbcQueue.length > 0) {
      const row = window.cbcQueue[0];
      const path = `events/${auth.currentUser.uid}/${row.sessionId}/${row.eventId}`;
      // Stable event path: retries replace this event rather than double-count it.
      await set(ref(db, path), { ...row, receivedAt: serverTimestamp() });
      window.cbcQueue.shift();
      window.cbcPersist();
    }
  } catch (error) {
    // Keep unacknowledged rows in the persistent queue and retry later.
    console.warn("Telemetry upload pending:", error.code || error.message);
  } finally {
    flushing = false;
  }
};
window.addEventListener("online", window.cbcFlush);
setInterval(window.cbcFlush, 5000);
window.cbcFlush();
