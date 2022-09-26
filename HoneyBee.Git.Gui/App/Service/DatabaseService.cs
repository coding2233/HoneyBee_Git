﻿using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Wanderer.Common;

namespace Wanderer.App.Service
{
    public interface IDatabaseService
    {
        LiteDatabase GetLiteDb(string name);

        void ReleaseDb(string name);

        void SetCustomerData<T>(string key, T value);
        T GetCustomerData<T>(string key, T defaultValue = default(T));
    }
    public class DatabaseService : IDatabaseService
    {
        private Dictionary<string, LiteDatabase> m_dbs = new Dictionary<string, LiteDatabase>();

        private string m_userDbPath;
        public DatabaseService()
        {
            m_userDbPath = Path.Combine(Application.UserPath, $"userdata.db");
        }

        public LiteDatabase GetLiteDb(string name)
        {
            LiteDatabase database = null;
            if (m_dbs.TryGetValue(name, out database))
            {
                if (database == null)
                {
                    m_dbs.Remove(name);
                }
            }

            if (database == null)
            {
                string dbPath = Path.Combine(Application.UserPath, $"{name}.db");
                database = new LiteDatabase(dbPath);
                m_dbs.Add(name, database);
            }

            return database;
        }

        public void ReleaseDb(string name)
        {
            if (m_dbs.TryGetValue(name, out LiteDatabase database))
            {
                m_dbs.Remove(name);

                database.Dispose();
                database = null;
            }
        }


        public void SetCustomerData<T>(string key, T value)
        {
            using (var db = new LiteDatabase(m_userDbPath))
            {
                var col = db.GetCollection<CustomerData<T>>(GetCustomerTableName<T>());
                CustomerData<T> customerData;
                var query = col.Query().Where(x => x.Key.Equals(key));
                if (query.Count() > 0)
                {
                    customerData = query.First();
                    customerData.Data = value;
                    col.Update(customerData);
                }
                else
                {
                    customerData = new CustomerData<T>()
                    {
                        Key = key,
                        Data = value
                    };
                    col.Insert(customerData);
                }
            }
        }
        public T GetCustomerData<T>(string key, T defaultValue = default(T))
        {
            using (var db = new LiteDatabase(m_userDbPath))
            {
                var col = db.GetCollection<CustomerData<T>>(GetCustomerTableName<T>());
                var value = col.Query().Where(x => x.Key.Equals(key));
                if (value.Count() > 0)
                {
                    return value.First().Data;
                }

                return defaultValue;
            }
        }

        public bool HasCustomerData<T>(string key)
        {
            using (var db = new LiteDatabase(m_userDbPath))
            {
                var col = db.GetCollection<CustomerData<T>>(GetCustomerTableName<T>());
                var value = col.Query().Where(x => x.Key.Equals(key));
                if (value.Count() > 0)
                {
                    return true;
                }
            }
            return false;
        }


        private string GetCustomerTableName<T>()
        {
            string tableName = Regex.Replace(typeof(T).Name, @"[^a-zA-Z0-9\u4e00-\u9fa5\s]", "");
            tableName = $"CustomerData_{tableName}";
            return tableName;
        }
    }



    class CustomerData<T>
    {
        //litedb必须带id
        public int Id { get; set; }
        public string Key { get; set; }
        public T Data { get; set; }
    }
}
