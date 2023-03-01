using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace AssetBundlePractice
{
    public class ABUpdateMgr : MonoBehaviour
    {
        private static ABUpdateMgr instance;
        public static ABUpdateMgr Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject obj = new GameObject("ABUpdateMgr");
                    instance = obj.AddComponent<ABUpdateMgr>();
                }
                return instance;
            }
        }
        
        //Ftp Info
        public static string ip;
        public static string userName;
        public static string password;

        //对比文件目录
        private readonly Dictionary<string, ABInfo> remoteABInfo = new();
        //存储本地AB包信息的字典
        private Dictionary<string, ABInfo> localABInfo = new();
        //待下载的文件的名字
        private List<string> downLoadList = new();
        public void CheckUpdate(UnityAction<bool> overCallBack, UnityAction<string> updateInfoCallBack)
        {
            remoteABInfo.Clear();
            localABInfo.Clear();
            downLoadList.Clear();
            //加载远端资源对比文件
            DownLoadABCompareFile(isOver =>
            {
                updateInfoCallBack("开始更新资源");
                if (isOver)
                {
                    updateInfoCallBack("对比文件下载结束");
                    updateInfoCallBack("解析远端对比文件");
                    GetRemoteABCompareFileInfo(out var fileInfo);
                    updateInfoCallBack("解析远端对比文件完成");
                    //加载本地资源对比文件
                    updateInfoCallBack("解析本地对比文件");
                    GetLocalABCompareFileInfo(over =>
                    {
                        if (over)
                        {
                            updateInfoCallBack("解析本地对比文件完成");
                            updateInfoCallBack("开始对比");
                            // 对比它们进行AB包下载
                            foreach (var abName in remoteABInfo.Keys)
                            {
                                // 1.判断那些资源是新的
                                if (!localABInfo.ContainsKey(abName))
                                    //记录要下载的新资源
                                    downLoadList.Add(abName);
                                else
                                {
                                    // 2.判断那些资源是需要更新的
                                    if (localABInfo[abName].md5 != remoteABInfo[abName].md5)
                                        // md5码不相等，需要更新，记录到带下载列表
                                        downLoadList.Add(abName);
                                    
                                    // 3.判断那些资源是需要删除的
                                    localABInfo.Remove(abName);
                                }
                            }

                            updateInfoCallBack("对比完成");
                            updateInfoCallBack("删除多余文件");
                            //删除没用的资源
                            foreach (var abName in localABInfo.Where(
                                         abName => 
                                             File.Exists(Application.persistentDataPath + "/" + abName)))
                            {
                                File.Delete(Application.persistentDataPath + "/" + abName);
                            }

                            updateInfoCallBack("删除完成");
                            updateInfoCallBack("下载和更新AB包文件");
                            DownLoadABFile(Over =>
                            {
                                if (Over)
                                {
                                    //下载完所有的AB包文件后
                                    //把本地的AB包对比文件更新为最新
                                    // 存储AB包对比文件
                                    updateInfoCallBack("更新AB包对比文件");
                                    File.WriteAllText(Application.persistentDataPath + "/ABCompareInfo.txt", fileInfo);
                                }
                                overCallBack(Over);
                            }, updateInfoCallBack);
                        }
                        else overCallBack(false);
                    });
                }
                else overCallBack(false);
            });
            
        }
        
        /// <summary>
        /// 下载AB包对比文件
        /// </summary>
        private async void DownLoadABCompareFile(UnityAction<bool> overCallBack)
        {
            //从资源服务器下载资源对比文件
            // www UniityWebRequest --http下载API
            //ftp相关api
            print(Application.persistentDataPath);
            //不能在子线程中访问Unity主线程的Application,所以把localPath声明到外部
            string localPath = Application.persistentDataPath + "/ABCompareInfo_TMP.txt";
            bool isOver = false;
            int count = 0;
            const int MaxCount = 5;
            while (count < MaxCount && !isOver)
            {
                await Task.Run(() =>
                {
                    isOver = DownloadFile("ABCompareInfo.txt", localPath);
                });
                count++;
            }
            overCallBack?.Invoke(isOver);
        }

        /// <summary>
        /// 解析远端获取的AB包文件
        /// </summary>
        private void GetRemoteABCompareFileInfo(out string fileInfos)
        {
            fileInfos = File.ReadAllText( Application.persistentDataPath + "/ABCompareInfo_TMP.txt");
            GetABCompareFileInfo(fileInfos, remoteABInfo);
        }

        /// <summary>
        /// 获取本地的AB包文件并解析
        /// </summary>
        private void GetLocalABCompareFileInfo(UnityAction<bool> overCallBack)
        {
            // 第一次进入这里是不会有文件的
            //如果Application.persistentDataPath文件夹中存在对比文件，说明之前已经下载更新过了
            if (File.Exists( Application.persistentDataPath + "/ABCompareInfo.txt"))
            {
                StartCoroutine(
                    GetLocalABCompareFileInfo("file:///" + Application.persistentDataPath + "/ABCompareInfo.txt",
                        overCallBack));
            }
            //如果Application.persistentDataPath文件夹中没有对比文件，就去Application.streamingAssetsPath中加载文件
            // 这里是默认资源文件，第一次从这里加载
            else if (File.Exists(Application.streamingAssetsPath + "/ABCompareInfo.txt"))
            {
                string path =
                    #if UNITY_ANDROID
                                 Application.streamingAssetsPath;
                    #else
                                "file:///" + Application.streamingAssetsPath;
                    #endif
                StartCoroutine(
                    GetLocalABCompareFileInfo(path + "/ABCompareInfo.txt",
                    overCallBack));
            }
            //如果两个都没进入就代表第一次进入且没有默认资源
            else overCallBack(true);
        }

        /// <summary>
        /// 下载AB包文件
        /// </summary>
        private async void DownLoadABFile(UnityAction<bool> overCallBack, UnityAction<string> updatePro)
        {
            // //遍历字典的key 根据文件名 下载AB包到本地
            // downLoadList = remoteABInfo.Keys.ToList();
            //本地存储路径
            string localPath = Application.persistentDataPath + "/";
            //记录下载文件数量和总数
            int downloadOverNum = 0, downloadMaxNum = downLoadList.Count,count = 0;
            //记录已下载文件
            List<string> tmpList = new();
            //最大重连次数
            const int DownLoadMaxNumber = 5;
            //下载循环
            while (downLoadList.Count > 0 && count < DownLoadMaxNumber)
            {
                foreach (var filename in downLoadList)
                {
                    //下载成功标识
                    var isOver = false;
                    await Task.Run(() => { isOver = DownloadFile(filename, localPath + filename); });
                    if (isOver)
                    {
                        updatePro($"{++downloadOverNum}/{downloadMaxNum}");
                        tmpList.Add(filename);
                    }
                }
                foreach (var item in tmpList) downLoadList.Remove(item);
                count++;
            }
            overCallBack(downLoadList.Count == 0);
        }

        private void GetABCompareFileInfo(string fileInfos , Dictionary<string, ABInfo> abInfos)
        {
            //解析AB包信息
            //获取资源对比文件中的字符串信息进行拆分
            string[] strings = fileInfos.Split("|");
            foreach (var str in strings)
            {
                var infos = str.Split(' ');
                //记录每一个远端AB包的信息
                abInfos.Add(infos[0], new ABInfo(infos[0], infos[1], infos[2]));
            }
        }

        private IEnumerator GetLocalABCompareFileInfo(string filePath, UnityAction<bool> overCallBack)
        {
            //通过UnityWebRequest去加载本地文件
            UnityWebRequest request = UnityWebRequest.Get(filePath);
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                //获取文件成功 继续往下执行
                GetABCompareFileInfo(request.downloadHandler.text, localABInfo);
                overCallBack(true);
            }
            else overCallBack(false);
        }

        private  bool DownloadFile(string fileName, string localPath)
        {
            try
            {
                string platform =
                #if UNITY_IOS
                    "IOS";
                #elif UNITY_ANDROID
                    "Android";
                #else
                    "PC";
                #endif
                print(ip);
                //创建ftp链接
                FtpWebRequest request =
                    WebRequest.Create(new Uri($"{ip}/{platform}/{fileName}")) as FtpWebRequest;
                //设置通信凭证
                NetworkCredential n = new NetworkCredential(userName, password);
                request!.Credentials = n;
                //其他设置
                // 设置代理为null
                request.Proxy = null;
                // 请求完毕后 是否关闭控制连接
                request.KeepAlive = false;
                // 下载指令
                request.Method = WebRequestMethods.Ftp.DownloadFile;
                // 指定下载类型-二进制
                request.UseBinary = true;
                // request.UsePassive = false;
                //下载文件
                // ftp的流对象
                FtpWebResponse response = request.GetResponse() as FtpWebResponse;
                Stream downloadStream = response!.GetResponseStream();
            
                // 读取文件信息写入流对象
                using FileStream file = File.Create(localPath);
                //每2kb读取
                byte[] bytes = new byte[2048];
                int content = downloadStream!.Read(bytes, 0, bytes.Length);

                //下载循环
                while (content != 0)
                {
                    //读取文件
                    file.Write(bytes, 0, content);
                    //读取完继续下载
                    content = downloadStream!.Read(bytes, 0, bytes.Length);
                }

                file.Close();
                downloadStream.Close();

                return true;
            }
            catch (Exception e)
            {
                Debug.Log(fileName + "下载失败：" + e.Message);
                return false;
            }
        }

        private void OnDestroy() => instance = null;

        private class ABInfo
        {
            //AB包信息类
            private string name;
            private long size;
            public readonly string md5;

            public ABInfo(string name, string size, string md5)
            {
                this.name = name;
                this.size = long.Parse(size);
                this.md5 = md5;
            }
        }
    }
}