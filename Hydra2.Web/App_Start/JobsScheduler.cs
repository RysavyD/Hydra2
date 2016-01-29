using System;   
using Hydra2.DownLoaders;
using Quartz;
using Quartz.Impl;
using Hydra2.Model;
using NLog;

namespace Hydra2.Web
{
    public class JobsScheduler
    {
        public static void Start()
        {
            IScheduler scheduler = StdSchedulerFactory.GetDefaultScheduler();
            scheduler.Start();

            IJobDetail Job1 = JobBuilder.Create<FirstJob>().Build();
            IJobDetail Job2 = JobBuilder.Create<SecondJob>().Build();
            IJobDetail Job3 = JobBuilder.Create<ThirdJob>().Build();
            IJobDetail Job4 = JobBuilder.Create<FourthJob>().Build();

            ITrigger Trigger1 = TriggerBuilder.Create()
                .WithIdentity("Trigger1")
                .WithSimpleSchedule(s => 
                    s.WithIntervalInHours(4)
                    .RepeatForever())
                .StartNow()
                .Build();

            ITrigger Trigger2 = TriggerBuilder.Create()
                .WithIdentity("Trigger2")
                .WithSimpleSchedule(s =>
                    s.WithIntervalInHours(4)
                    .RepeatForever())
                .StartAt(DateTimeOffset.Now.AddHours(1))
                .Build();


            ITrigger Trigger3 = TriggerBuilder.Create()
                .WithIdentity("Trigger3")
                .WithSimpleSchedule(s =>
                    s.WithIntervalInHours(4)
                    .RepeatForever())
                .StartAt(DateTimeOffset.Now.AddHours(2))
                .Build();

            ITrigger Trigger4 = TriggerBuilder.Create()
                .WithIdentity("Trigger4")
                .WithSimpleSchedule(s =>
                    s.WithIntervalInHours(4)
                    .RepeatForever())
                .StartAt(DateTimeOffset.Now.AddHours(3))
                .Build();

            scheduler.ScheduleJob(Job1, Trigger1);
            scheduler.ScheduleJob(Job2, Trigger2);
            scheduler.ScheduleJob(Job3, Trigger3);
            scheduler.ScheduleJob(Job4, Trigger4);
        }


        [DisallowConcurrentExecution]
        public class FirstJob : IJob
        {
            public void Execute(IJobExecutionContext context)
            {
                NLogger.Log(LogLevel.Info, "Job 1 Start");
                Update.UpdateSpots(1, 200);
                NLogger.Log(LogLevel.Info, "Job 1 End");
            }
        }

        [DisallowConcurrentExecution]
        public class SecondJob : IJob
        {
            public void Execute(IJobExecutionContext context)
            {
                NLogger.Log(LogLevel.Info, "Job 2 Start");
                Update.UpdateSpots(201, 400);
                NLogger.Log(LogLevel.Info, "Job 2 End");
            }
        }

        [DisallowConcurrentExecution]
        public class ThirdJob : IJob
        {
            public void Execute(IJobExecutionContext context)
            {
                NLogger.Log(LogLevel.Info, "Job 3 Start");
                Update.UpdateSpots(401, 600);
                NLogger.Log(LogLevel.Info, "Job 3 End");
            }
        }

        [DisallowConcurrentExecution]
        public class FourthJob : IJob
        {
            public void Execute(IJobExecutionContext context)
            {
                NLogger.Log(LogLevel.Info, "Job 4 Start");
                Update.UpdateSpots(601, 800);
                NLogger.Log(LogLevel.Info, "Job 4 End");
            }
        }
    }
}