using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Infrastructure.PackMethod
{
    public static class FTPHelper
    {
        /// <summary>
        /// FTP上传
        /// </summary>
        /// <param name="ftpUrl">ftp路径</param>
        /// <param name="username">ftp登录用户名</param>
        /// <param name="password">ftp登录密码</param>
        /// <param name="filePath">上传的文件路径</param>
        /// <param name="fileName">上传到FTP文件名称</param>
        /// <returns></returns>
        public static string UploadFile(string ftpUrl, string username, string password, string filePath, string fileName = null)
        {
			try
			{
				//获取文件信息
				FileInfo fileInfo = new FileInfo(filePath);
				FtpWebRequest request=(FtpWebRequest)WebRequest.Create(ftpUrl);
				request.Method = WebRequestMethods.Ftp.UploadFile;
                request.UseBinary = true; // 使用二进制传输方式
                request.UsePassive = false; // 使用主动模式连接（在某些服务器上需要）
                request.KeepAlive = false; // 关闭连接后释放套接字，避免端口耗尽问题
                request.ContentLength = fileInfo.Length;
                byte[] fileContents = File.ReadAllBytes(filePath);
                // 写入文件内容到请求流中
                using (Stream requestStream = request.GetRequestStream())
                { 
                    requestStream.Write(fileContents, 0, fileContents.Length);
                }
                // 获取响应并关闭连接
                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse()) 
                {
                    return $"Upload File Complete, status {response.StatusDescription} @$#@#$@#$#";
                }
            }
            catch (Exception ex)
			{
                return $"FTP上传失败：{ex.Message}";
            }
        }
        /// <summary>
        /// FTP下载
        /// </summary>
        /// <param name="ftpUrl">FTP路径</param>
        /// <param name="username">ftp登录用户名</param>
        /// <param name="password">ftp登录密码</param>
        /// <param name="filePath">下载到本地的路径</param>
        /// <param name="fileName">下载到本地路径的文件名称</param>
        /// <returns></returns>
        public static string Download(string ftpUrl, string username, string password, string filePath, string fileName = null)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName)) fileName = Path.GetFileName(ftpUrl);
                FileInfo fileInfo = new FileInfo(filePath);
                FtpWebRequest reqFTP;
                reqFTP=(FtpWebRequest)FtpWebRequest.Create(new Uri(ftpUrl));
                reqFTP.Credentials=new NetworkCredential(username, password);
                reqFTP.Method = WebRequestMethods.Ftp.GetFileSize;
                long cl = 0;
                using (FtpWebResponse response=(FtpWebResponse)reqFTP.GetResponse())
                {
                    cl = response.ContentLength;
                }
                int bufferSize = Convert.ToInt32((double)cl);
                int readCount;
                byte[] buffer=new byte[bufferSize];
                reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpUrl));
                reqFTP.Credentials = new NetworkCredential(username,password);
                reqFTP.Method = WebRequestMethods.Ftp.DownloadFile;
                using (FtpWebResponse response =(FtpWebResponse)reqFTP.GetResponse())
                {
                    Stream ftpStream = response.GetResponseStream();
                    readCount = ftpStream.Read(buffer,0,bufferSize);
                    FileStream outputStream = new FileStream(filePath + "\\" + fileName, FileMode.Create);
                    while (readCount > 0) 
                    {
                        outputStream.Write(buffer,0, readCount);
                        readCount = ftpStream.Read(buffer,0,bufferSize);
                    }
                    ftpStream.Close();
                    outputStream.Close();
                    response.Close();
                    return $"Down File Complete, status {response.StatusDescription} @$#@#$@#$#";
                }
            }
            catch (Exception ex)
            {
                return $"FTP下载失败：{ex.Message} @$#@#$@#$#";
            }
        }
    }
}
