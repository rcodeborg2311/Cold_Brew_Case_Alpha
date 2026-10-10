// Load this classic script BEFORE Unity's loader in the Web template.
(function () {
  const key = "cold-brew-case-telemetry-v1";
  let saved = [];
  try { saved = JSON.parse(localStorage.getItem(key) || "[]"); } catch (_) {}
  window.cbcQueue = Array.isArray(saved) ? saved : [];
  window.cbcPersist = function () {
    try { localStorage.setItem(key, JSON.stringify(window.cbcQueue)); } catch (_) {}
  };
  window.cbcEnqueue = function (row) {
    if (window.cbcQueue.length >= 500) {
      console.warn("Telemetry queue is full; gameplay continues.");
      return;
    }
    window.cbcQueue.push(row);
    window.cbcPersist();
    if (typeof window.cbcFlush === "function") window.cbcFlush();
  };
})();
