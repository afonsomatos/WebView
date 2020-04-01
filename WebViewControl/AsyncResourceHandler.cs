using System.IO;
using System.Text;
using Xilium.CefGlue;
using Xilium.CefGlue.Common.Handlers;

namespace WebViewControl {

    internal class AsyncResourceHandler : DefaultResourceHandler {

        private class InternalTextStream : MemoryStream {

            public InternalTextStream(int capacity) : base(capacity) { }

            public InternalTextStream(byte[] buffer) : base(buffer) { }
        }

        private CefCallback responseCallback;
        private bool autoDisposeStream;

        protected override RequestHandlingFashion ProcessRequestAsync(CefRequest request, CefCallback callback) {
            if (Response == null && string.IsNullOrEmpty(RedirectUrl)) {
                responseCallback = callback;
                return RequestHandlingFashion.ContinueAsync;
            }
            WrapResponseStream();
            return RequestHandlingFashion.Continue;
        }

        public void SetResponse(string response, string mimeType = null) {
            Response = GetMemoryStream(response, Encoding.UTF8, includePreamble: true);
            MimeType = mimeType;
        }

        public void SetResponse(Stream response, string mimeType = null, bool autoDisposeStream = false) {
            Response = response;
            MimeType = mimeType;
            this.autoDisposeStream = autoDisposeStream;
        }

        public void RedirectTo(string targetUrl) {
            RedirectUrl = targetUrl;
        }

        public void Continue() {
            if (responseCallback != null) {
                using (responseCallback) {
                    WrapResponseStream();
                    responseCallback.Continue();
                }
            }
        }

        public static DefaultResourceHandler FromText(string text) {
            var handler = new AsyncResourceHandler();
            handler.SetResponse(text);
            return handler;
        }

        private static MemoryStream GetMemoryStream(string text, Encoding encoding, bool includePreamble = true) {
            if (includePreamble) {
                var preamble = encoding.GetPreamble();
                var bytes = encoding.GetBytes(text);

                var memoryStream = new InternalTextStream(preamble.Length + bytes.Length);

                memoryStream.Write(preamble, 0, preamble.Length);
                memoryStream.Write(bytes, 0, bytes.Length);

                memoryStream.Position = 0;

                return memoryStream;
            }

            return new InternalTextStream(encoding.GetBytes(text));
        }

        private void WrapResponseStream() {
            //if (Response == null || Response is InternalTextStream) {
            //    return;
            //}

            //var streamCopy = new MemoryStream();
            //lock (Response) {
            //    Response.Position = 0;
            //    Response.CopyTo(streamCopy);
            //}
            
            //if (autoDisposeStream) {
            //    Response.Dispose();
            //}

            //Response = streamCopy;
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            if (autoDisposeStream) {
                Response?.Dispose();
            }
        }
    }
}
