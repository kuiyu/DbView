
    /// <summary>
    /// 日志记录工具
    /// </summary>
    public static partial class LogHelper
    {

        public static void Info(string message, string tag = "")
        {
            try
            {
                message = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + "\r\n";
                WriteFile(message, tag);
            }
            catch (Exception e)
            {
                WriteFile(e.Message);
            }

        }
         
        static void WriteFile(string s, string tag = "")
        {
            try
            {
                var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "logs");
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                var file = Path.Combine(dir, DateTime.Now.ToString("yyyy-MM-dd") + ".txt");
                if (!string.IsNullOrEmpty(tag))
                {
                    file = Path.Combine(dir, tag + DateTime.Now.ToString("yyyy-MM-dd") + ".txt");
                }
                File.AppendAllText(file, s);
            }
            catch (Exception e)
            {

            }

        }

       
 


    }


