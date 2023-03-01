using System;
using System.IO;
using UnityEngine;

namespace AssetBundlePractice
{
    public class Main : MonoBehaviour
    {
        private void Start()
        {
           ABUpdateMgr.Instance.CheckUpdate(
               isOver => { print(isOver ? "检测更新结束" : "请检查网络"); }, 
               //处理更新相关逻辑
               print);
        }
    }
}