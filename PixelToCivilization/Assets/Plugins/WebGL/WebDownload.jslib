// V6.1.2 WebGL 浏览器端文件下载（存档导出 .json）
mergeInto(LibraryManager.library, {
  PxDownloadFile: function (namePtr, textPtr) {
    try {
      var name = UTF8ToString(namePtr);
      var text = UTF8ToString(textPtr);
      var blob = new Blob([text], { type: 'application/json;charset=utf-8' });
      var url = URL.createObjectURL(blob);
      var a = document.createElement('a');
      a.href = url; a.download = name;
      document.body.appendChild(a); a.click(); a.remove();
      setTimeout(function () { URL.revokeObjectURL(url); }, 1500);
    } catch (e) { console.error('PxDownloadFile failed', e); }
  }
});
