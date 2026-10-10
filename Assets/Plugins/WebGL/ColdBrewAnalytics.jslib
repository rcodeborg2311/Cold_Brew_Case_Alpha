mergeInto(LibraryManager.library, {
  CBC_Enqueue: function (jsonPtr) {
    var row = JSON.parse(UTF8ToString(jsonPtr));
    if (typeof window.cbcEnqueue === "function") {
      window.cbcEnqueue(row);
    } else {
      console.error("Analytics bootstrap was not loaded before Unity.");
    }
  }
});
