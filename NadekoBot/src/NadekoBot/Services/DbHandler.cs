﻿using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NadekoBot.Services.Database;

namespace NadekoBot.Services
{
    public class DbHandler
    {
        private static DbHandler _instance = null;
        public static DbHandler Instance = _instance ?? (_instance = new DbHandler());
        private readonly DbContextOptions options;

        private string connectionString { get; }

        static DbHandler() { }

        private DbHandler()
        {
            connectionString = NadekoBot.Credentials.Db.ConnectionString;
            var optionsBuilder = new DbContextOptionsBuilder();
            optionsBuilder.UseSqlite(NadekoBot.Credentials.Db.ConnectionString);
            options = optionsBuilder.Options;
            //switch (NadekoBot.Credentials.Db.Type.ToUpperInvariant())
            //{
            //    case "SQLITE":
            //        dbType = typeof(NadekoSqliteContext);
            //        break;
            //    //case "SQLSERVER":
            //    //    dbType = typeof(NadekoSqlServerContext);
            //    //    break;
            //    default:
            //        break;

            //}
        }

        public NadekoContext GetDbContext()
        {
            var context = new NadekoContext(options);
            context.Database.Migrate();
            context.EnsureSeedData();

            return context;
        }

        private IUnitOfWork GetUnitOfWork() =>
            new UnitOfWork(GetDbContext());

        public static IUnitOfWork UnitOfWork() =>
            DbHandler.Instance.GetUnitOfWork();
    }
}