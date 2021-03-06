using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LibCommon;
using LibCommon.Structs.DBModels;
using LibLogger;

namespace AKStreamWeb.AutoTask
{
    public class AutoTaskOther
    {
        private long count = 0;

        private void run()
        {
            while (true)
            {
                if (count > 90000000000)
                {
                    count = 0;
                }

                count++;
                try
                {
                    if (count % 3600 == 0) //3600秒一次
                    {
                        doDeleteFor24HourAgo(); //删除24小时前被软删除的过期失效的文件
                    }

                    if (count % 3600 == 0) //3600秒一次
                    {
                        foreach (var mediaServer in Common.MediaServerList)
                        {
                            if (mediaServer != null && mediaServer.KeeperWebApi != null && mediaServer.IsKeeperRunning)
                            {
                                mediaServer.KeeperWebApi.CleanUpEmptyDir(out _);
                            }
                        }
                    }
                }
                catch
                {
                    //
                }

                Thread.Sleep(1000);
            }
        }

        private void doDeleteFor24HourAgo()
        {
            try
            {
                List<RecordFile> retList = null!;
                retList = ORMHelper.Db.Select<RecordFile>()
                    .Where(x => x.Deleted == true)
                    .Where(x => x.Undo == true)
                    .Where(x => ((DateTime) x.UpdateTime!).AddHours(24) <= DateTime.Now)
                    .ToList();

                if (retList != null && retList.Count > 0)
                {
                    var deleteFileList = retList.Select(x => x.VideoPath).ToList();
                    var deleteFileIdList = retList.Select(x => x.Id).ToList();

                    var mediaServer =
                        Common.MediaServerList.FindLast(x => x.MediaServerId.Equals(retList[0].MediaServerId));
                    if (mediaServer != null && mediaServer.IsKeeperRunning)
                    {
                        var delRet = mediaServer.KeeperWebApi.DeleteFileList(out _, deleteFileList);

                        /*foreach (var ret in retList)
                        {
                            var o = delRet.PathList.FindLast(x => x.Equals(ret.VideoPath));
                            if (string.IsNullOrEmpty(o))
                            {
                                var o2 = deleteFileIdList.FindLast(x => x.Equals(ret.Id));
                                if (o2 != null && o2 > 0)
                                {
                                    deleteFileIdList.Remove(o2);
                                }
                            }
                        }*/ //这里过滤掉未删除文件的话，会反复删除失败，去掉这段逻辑

                        if (deleteFileIdList != null && deleteFileIdList.Count > 0)
                        {
                            var a = ORMHelper.Db.Update<RecordFile>().Set(x => x.UpdateTime, DateTime.Now)
                                .Set(x => x.Undo, false)
                                .Where(x => deleteFileIdList.Contains(x.Id)).ExecuteAffrows();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(
                    $"[{Common.LoggerHead}]->删除被软删除记录文件时发生异常->{ex.Message}->{ex.StackTrace}");
            }
        }

        public AutoTaskOther()
        {
            new Thread(new ThreadStart(delegate
            {
                try
                {
                    run();
                }
                catch
                {
                }
            })).Start();
        }
    }
}