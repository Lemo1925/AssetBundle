using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AssetBundlePractice;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Editor
{
    public class ABTools : EditorWindow
    {
        //FtpConfig
        private string ip = "ftp://127.0.0.1";
        private string userName = "AssetBundle";
        private string password = "Password";
        
        private int nowSelectIndex;
        private string[] targetStr = { "PC", "IOS", "Android" };
    
        [MenuItem("AB包工具/打开工具窗口")]
        private static void OpenWindow()
        {
            ABTools window = EditorWindow.GetWindowWithRect(typeof(ABTools), 
                new Rect(0, 0, 360, 250)) as ABTools;
            window!.Show();
            
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(10,10,150,20), "平台选择");
            //页签显示是从数组中取出字符串内容来显示所以需要改变当前选中的Index
            nowSelectIndex = GUI.Toolbar(new Rect(100, 10, 250, 20), nowSelectIndex, targetStr);
            //服务器IP显示
            GUI.Label(new Rect(10, 40, 150,20),"ftp服务器IP");
            ip = GUI.TextField(new Rect(100, 40, 250, 20), ip);
            ABUpdateMgr.ip =ip;
            //服务器用户名
            GUI.Label(new Rect(10, 70, 150,20),"服务器用户名");
            ABUpdateMgr.userName = GUI.TextField(new Rect(100, 70, 250, 20), userName);
            userName = ABUpdateMgr.userName;
            //服务器密码
            GUI.Label(new Rect(10, 100, 150,20),"服务器密码");
            ABUpdateMgr.password = GUI.TextField(new Rect(100, 100, 250, 20), password);
            password = ABUpdateMgr.password;
            //创建对比文件按钮
            // ReSharper disable once Unity.PerformanceCriticalCodeInvocation
            if (GUI.Button(new Rect(10, 140, 150, 40), "创建对比文件")) CreateABCompareFile();
            //保存默认资源到StreamingAssets按钮
            if (GUI.Button(new Rect(200, 140, 150, 40), "保存默认资源")) MoveAB2StreamingAssets();
            //上传AB包文件按钮
            // ReSharper disable once Unity.PerformanceCriticalCodeInvocation
            if (GUI.Button(new Rect(105, 200, 150, 40), "上传AB包文件")) UpLoadAllABFile();
        }

        private void CreateABCompareFile()
        {
            //获取文件夹信息
            DirectoryInfo directory = Directory.CreateDirectory(Application.dataPath + $"/Resource/ArtRes/AB/{targetStr[nowSelectIndex]}");
            //获取所有文件信息
            FileInfo[] fileInfos = directory.GetFiles();
            //声明用于存储信息的字符串
            string abCompareInfo = "";
            
            foreach (var fileInfo in fileInfos)
                //AB包信息
                if (fileInfo.Extension == "")
                    //拼接AB包信息
                    abCompareInfo += fileInfo.Name + " " + fileInfo.Length + " " + GetMD5(fileInfo.FullName) + '|';
            //去掉最后一个包的终结符
            abCompareInfo = abCompareInfo.Substring(0, abCompareInfo.Length - 1);

            //存储AB包信息
            File.WriteAllText(Application.dataPath + $"/Resource/ArtRes/AB/{targetStr[nowSelectIndex]}/ABCompareInfo.txt", abCompareInfo);
            AssetDatabase.Refresh();
            Debug.Log("AB包对比文件存储成功");
        }

        private void MoveAB2StreamingAssets()
        {
            Object[] selectedAsset = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);
            if (selectedAsset.Length == 0) return;
            string abCompareInfo = "";
            
            foreach (var Obj in selectedAsset)
            {
                //获取资源的路径
                string assetPath = AssetDatabase.GetAssetPath(Obj);
                string fileName = assetPath.Substring(assetPath.LastIndexOf('/'));
                //带点符号的文件不处理
                if (fileName.IndexOf('.') != -1) continue;
                
                //利用AssetDataBase中的API将选中文件复制到目标路径
                AssetDatabase.CopyAsset(assetPath, "Assets/StreamingAssets" + fileName);
                //获取文件信息
                FileInfo fileInfo = new(Application.streamingAssetsPath +fileName);
                //拼接AB包信息，得到本地对比文件
                abCompareInfo += fileInfo.Name + " " + fileInfo.Length + " " +
                                 GetMD5(fileInfo.FullName) + "|";
            }
            //方便拆分
            abCompareInfo = abCompareInfo.Substring(0, abCompareInfo.Length - 1);
            //将信息存入文件
            File.WriteAllText(Application.streamingAssetsPath + "/ABCompareInfo.txt", abCompareInfo);
            AssetDatabase.Refresh();
        }

        private void UpLoadAllABFile()
        {
            //获取文件夹信息
            DirectoryInfo directory = Directory.CreateDirectory(Application.dataPath + $"/Resource/ArtRes/AB/{targetStr[nowSelectIndex]}/");
            //获取所有文件信息
            FileInfo[] fileInfos = directory.GetFiles();
            
            foreach (var fileInfo in fileInfos)
                //AB包信息和资源对比文件.txt
                if (fileInfo.Extension is "" or ".txt")
                    //上传文件
                    FtpUploadFile(fileInfo.FullName, fileInfo.Name);
        }

        private async void FtpUploadFile(string filePath, string fileName)
        {
            await Task.Run(() =>
            {
                try
                { 
                    //创建ftp链接
                    FtpWebRequest request =
                        WebRequest.Create(new Uri($"{ABUpdateMgr.ip}/{targetStr[nowSelectIndex]}/{fileName}")) as FtpWebRequest;
                    //设置通信凭证
                    NetworkCredential n = new NetworkCredential("AssetBundle", "6EBDPDKG3cmz3tkG");
                    request!.Credentials = n;
                    //其他设置
                    // 设置代理为null
                    request.Proxy = null;
                    // 请求完毕后 是否关闭控制连接
                    request.KeepAlive = false;
                    // 上传指令
                    request.Method = WebRequestMethods.Ftp.UploadFile;
                    // 指定上传类型-二进制
                    request.UseBinary = true;
                    // request.UsePassive = false;
                    //上传文件
                    // ftp的流对象
                    Stream upLoadStream = request.GetRequestStream();
                    // 读取文件信息写入流对象
                    using (FileStream file = File.OpenRead(filePath))
                    {
                        //2kb上传
                        byte[] bytes = new byte[2048];
                        int content = file.Read(bytes, 0, bytes.Length);

                        //上传循环
                        while (content != 0)
                        {
                            //写完上传
                            upLoadStream.Write(bytes, 0, content);
                            //上传完读取
                            content = file.Read(bytes, 0, bytes.Length);
                        }

                        file.Close();
                        upLoadStream.Close();
                    }
                    Debug.Log(fileName + "文件上传成功");
                }
                catch (Exception e)
                {
                    Debug.Log(fileName + "上传失败" + e.Message);
                }
            });
        }
        
        private string GetMD5(string filePath)
        {
            using FileStream file = new FileStream(filePath,FileMode.Open);
            //创建一个用于存储 md5码的对象
            MD5 md5 = new MD5CryptoServiceProvider();
            //利用 API得到字节数组
            byte[] md5Info = md5.ComputeHash(file);
            
            file.Close();
            //将字节数组形式的 MD5码转为 16进制
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var code in md5Info)
                stringBuilder.Append(code.ToString("x2"));

            return stringBuilder.ToString();
        }
       
    }
}