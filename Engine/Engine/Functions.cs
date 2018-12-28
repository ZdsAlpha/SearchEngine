using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Engine
{
    static class Functions
    {
        public static void Response(HttpListenerResponse response, string content)
        {
            try
            {
                var stream = response.OutputStream;
                byte[] data = Encoding.UTF8.GetBytes(content);
                response.ContentLength64 = data.Length;
                stream.Write(data, 0, data.Length);
                stream.Flush();
                stream.Close();
                response.Close();
            }
            catch
            {
            }
        }
    }
}
