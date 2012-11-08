using System;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Threading;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Data.SqlClient;
using System.Data;
using System.Collections.Generic;
namespace multithreadmailer
{
    
    public class SimpleAsync
    {
        static List<string> lines = new List<string>();
        static int linecount = 0;
        private static readonly object _syncObject = new object();
        static readonly TextWriter tw;
      
        static SimpleAsync()
        {
            //Write timestamp-named log file to current directory
            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            string fileName = string.Format("MailLog-{0:yyyy-MM-dd_hh-mm-ss-tt}.txt",DateTime.Now);
            string fullPath = Path.Combine(appPath, fileName);
            tw = TextWriter.Synchronized(File.AppendText(fullPath));
            
        }

        public static void Log(string logMessage, TextWriter w)
        {
            // only one thread can own this lock, so other threads
            // entering this method will wait here until lock is
            // available.
            lock (_syncObject)
            {
                linecount++;
                w.WriteLine("{0} {1} {2}", linecount.ToString("D5"), logMessage, DateTime.Now.ToString("yyyy.MM.dd hh:mm:ss"));
                // Update the underlying file.
                w.Flush();
            }
        }
        //static bool mailSent = false;


        private static void SendCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            // Get the unique identifier for this asynchronous operation.
            String token = (string)e.UserState;

            if (e.Cancelled)
            {
                Console.WriteLine("[{0}] Send canceled.", token);
            }
            if (e.Error != null)
            {
                Console.WriteLine("[{0}] {1}", token, e.Error.ToString());
            }
            else
            {
                Console.WriteLine("Message sent: {0}", DateTime.Now);
                Log(e.UserState.ToString(), tw);
            }
            //mailSent = true;
        }


        
        public static void Main(string[] args)
        {
            //download newswire page
            WebClient wclient = new WebClient();
            Byte[] pageData = wclient.DownloadData("http://newswire.uark.edu/email/default.aspx");
            string pageHtml = Encoding.ASCII.GetString(pageData);

            //Read email adresses from text file in the current directory
            //Useful for testing purposes

            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            string fileName = "emaillist.txt";
            string fullPath = Path.Combine(appPath, fileName);
            string[] lines = File.ReadAllLines(fullPath);
            int numRemaining = lines.Length;


            //Read email adresses SQL into memory
            
            //using (SqlConnection cnn = new SqlConnection("server=(local);database=Newswire;Integrated Security=SSPI"))
            //{
            //    SqlDataAdapter da = new SqlDataAdapter("select email from emailTest", cnn);
            //    DataSet ds = new DataSet();
            //    da.Fill(ds, "emailTest");
            //    foreach (DataRow row in ds.Tables["emailTest"].Rows)
            //    {
            //        lines.Add(row["email"].ToString());
            //    }
            //}
            //int numRemaining = lines.Count;

            

            
            using (ManualResetEvent waitHandle = new ManualResetEvent(numRemaining == 0))
            {
                object numRemainingLock = new object();
                foreach (var user in lines)
                {
                    SmtpClient client = new SmtpClient(args[0]);
                    client.SendCompleted += SendCompletedCallback;
                    MailAddress from = new MailAddress("newswire@uark.edu");
                    MailAddress to = new MailAddress(user);
                    MailMessage message = new MailMessage(from, to);
                    try
                    {
                      
                        message.IsBodyHtml = true;
                        message.Body = (pageHtml);
                        message.BodyEncoding = Encoding.UTF8;
                        message.Subject = "Arkansas Newswire Headlines";
                        message.SubjectEncoding = System.Text.Encoding.UTF8;
                        string userState = String.Format(user);
                        client.SendCompleted += delegate
                        {
                            lock (numRemainingLock)
                            {
                                if (--numRemaining == 0)
                                {
                                    waitHandle.Set();
                                }
                            }
                        };
                        client.SendAsync(message, userState);
                    }
                    catch
                    {
                        message.Dispose();
                        throw;
                    }
                }
                waitHandle.WaitOne();
            }
        }
    }
}