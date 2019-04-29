﻿using CommonLibrary;
using Data.Local.Common;
using Data.Local.Contexts;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Local.Services
{
    public abstract class BaseService<T,TKey> where T:BaseEntity<TKey> 
    {
        protected virtual DbContextType DbContextType { get; set; } = DbContextType.App;

        private DbContextFactory _dbContextFactory;

        public BaseService(DbContextFactory dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        protected DbContext GetDbContext()
        {
            return _dbContextFactory.GetDbContext(DbContextType);
        }

        public async Task<T> GetLogAsync(TKey id)
        {
            using(var dbContext = GetDbContext())
            {
                dbContext.Set<T>().Where(r => r.Id == id).FirstOrDefaultAsync();
            }
            return await Logs.Where(r => r.Id == id).FirstOrDefaultAsync();
        }

        public async Task<IList<T>> GetLogsAsync(int skip, int take, DataRequest<T> request)
        {
            IQueryable<T> items = GetLogs(request);

            // Execute
            var records = await items.Skip(skip).Take(take)
                .AsNoTracking()
                .ToListAsync();

            return records;
        }

        public async Task<IList<T>> GetLogKeysAsync(int skip, int take, DataRequest<T> request)
        {
            IQueryable<T> items = GetLogs(request);

            // Execute
            var records = await items.Skip(skip).Take(take)
                .Select(r => new T
                {
                    Id = r.Id,
                })
                .AsNoTracking()
                .ToListAsync();

            return records;
        }


        private IQueryable<T> GetLogs(DataRequest<T> request)
        {
            IQueryable<T> items = Logs;

            // Query
            if (!String.IsNullOrEmpty(request.Query))
            {
                items = items.Where(r => r.Message.Contains(request.Query.ToLower()));
            }

            // Where
            if (request.Where != null)
            {
                items = items.Where(request.Where);
            }

            // Order By
            if (request.OrderBy != null)
            {
                items = items.OrderBy(request.OrderBy);
            }
            if (request.OrderByDesc != null)
            {
                items = items.OrderByDescending(request.OrderByDesc);
            }

            return items;
        }

        public async Task<int> GetLogsCountAsync(DataRequest<T> request)
        {
            IQueryable<T> items = Logs;

            // Query
            if (!String.IsNullOrEmpty(request.Query))
            {
                items = items.Where(r => r.Message.Contains(request.Query.ToLower()));
            }

            // Where
            if (request.Where != null)
            {
                items = items.Where(request.Where);
            }

            return await items.CountAsync();
        }

        public async Task<int> CreateLogAsync(AppLog appLog)
        {
            appLog.DateTime = DateTime.UtcNow;
            Entry(appLog).State = EntityState.Added;
            return await SaveChangesAsync();
        }

        public async Task<int> DeleteLogsAsync(params T[] logs)
        {
            Logs.RemoveRange(logs);
            return await SaveChangesAsync();
        }

        public async Task MarkAllAsReadAsync()
        {
            var items = await Logs.Where(r => !r.IsRead).ToListAsync();
            foreach (var item in items)
            {
                item.IsRead = true;
            }
            await SaveChangesAsync();
        }
    }

}