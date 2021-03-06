﻿using Hangfire;
using Domotica_API.Models;
using Microsoft.AspNetCore.Builder;

namespace Domotica_API.Schedules
{
    public static class Scheduler
    {
        public static void Initialize()
        {
            //Add your tasks here.

            //Do task hourly.
            RecurringJob.AddOrUpdate(() => new TokenCleaner().Run(), Cron.Hourly);
           
            RecurringJob.AddOrUpdate(() => new OnlineUsers().Run(), Cron.Minutely);
        }
    }
}
