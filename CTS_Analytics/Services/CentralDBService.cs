﻿using CTS_Models;
using CTS_Models.DBContext;
using System;
using System.Data.Entity;
using System.Collections.Generic;
using System.Linq;
using StackExchange.Profiling;

namespace CTS_Analytics.Services
{
    public class CentralDBService
    {
        private readonly CtsDbContext cdb;


        public CentralDBService()
        {
            this.cdb = new CtsDbContext();

        }

        public decimal GetPlanData(string location, DateTime fromDate, DateTime toDate)
        {
            var planSum = cdb.LocalPlansBWithLocationID
                .Where(l => location.Contains(l.LocationID))
                .Where(d => d.Date >= fromDate && d.Date <= toDate)
                .Select(p => p.Plan)
                .DefaultIfEmpty()
                .Sum();

            if (location == "sar1" || location == "sar3")
                planSum = planSum / 2;

            return planSum;
        }

        public int GetStuffCount(int location, DateTime fromDate, DateTime toDate)
        {
            var staffnum = cdb.LocalStaffs
                    .Where(s => s.ShopID == location)
                    .Where(d => d.Date <= toDate && d.Date >= fromDate)
                    .Select(v => v.CNT)
                    .DefaultIfEmpty()
                    .Select(int.Parse)
                    .ToArray()
                    .Sum();

            return staffnum;
        }

        public Location FindLocationByLocationID(string locationId)
        {
            return cdb.Locations.Find(locationId);
        }

        public TEquip FindEquipmentById<TEquip>(int Id) where TEquip : class, IEquip
        {
            var bd = new CtsEquipContext<TEquip>();
            return bd.DbSet.Find(Id);
        }

        public DateTime GetStartShiftTime(string locationId)
        {
            var nowTime = TimeSpan.Parse(System.DateTime.Now.ToString("HH:mm:ss"));
            var times = cdb.Shifts
                .Where(l => l.LocationID == locationId)
                .ToArray();
            if (times.Length == 0)
                return DateTime.Today;

            var startTime = times
                .Where(t => t.TimeStart < nowTime)
                .OrderByDescending(c => c.TimeStart)
                .Select(f => f.TimeStart)
                .FirstOrDefault();
            if (startTime == default(TimeSpan))
            {
                nowTime = new TimeSpan(23, 59, 59);
                startTime = times
                .Where(t => t.TimeStart < nowTime)
                .OrderByDescending(c => c.TimeStart)
                .Select(f => f.TimeStart)
                .First();
                return DateTime.Today.AddDays(-1).Add(startTime);
            }

            return DateTime.Today.Add(startTime);
        }

        public DateTime GetEndShiftTime(string locationId, DateTime startShiftTime)
        {
            var nowTime = TimeSpan.Parse(startShiftTime.ToString("HH:mm:ss"));
            var times = cdb.Shifts
                .Where(l => l.LocationID == locationId)
                .ToArray();
            if (times.Length == 0)
                return startShiftTime.AddHours(24);

            var endTime = times
                .Where(t => t.TimeStart > nowTime)
                .OrderByDescending(c => c.TimeStart)
                .Select(f => f.TimeStart)
                .LastOrDefault();

            if (endTime == default(TimeSpan))
            {
                nowTime = new TimeSpan(0, 00, 1);
                endTime = times
                .Where(t => t.TimeStart > nowTime)
                .OrderByDescending(c => c.TimeStart)
                .Select(f => f.TimeStart)
                .Last();
                return DateTime.Today.AddDays(1).Add(endTime);
            }

            return System.DateTime.Today.Add(endTime);
        }

        public IQueryable<TTransfer> GetTransfers<TTransfer>(int Id, DateTime fromDate, DateTime toDate) where TTransfer : class, ITransfer
        {
            var db = new CtsTransferContext<TTransfer>();
            return db.DbSet
               .Where(s => s.EquipID == Id)
               .Where(d => d.TransferTimeStamp >= fromDate && d.TransferTimeStamp <= toDate)
               .Where(v => v.IsValid == true)
               .OrderByDescending(t => t.TransferTimeStamp)
               .AsNoTracking();
        }

        public IQueryable<WagonTransfer> GetWagonTransfersSendFromMine(Location toDestLocation, DateTime fromDate, DateTime toDate, string fromMineId)
        {
            var db = new CtsTransferContext<WagonTransfer>();
            return db.DbSet
            .Where(t => t.Equip.Location.ID == fromMineId && t.IsValid == true)
            .Where(t => t.ToDest.Contains(toDestLocation.LocationName) || t.ToDest == toDestLocation.ID)
            .Where(t => t.Direction == CTS_Core.ProjectConstants.WagonDirection_FromObject)
            .Where(t => t.TransferTimeStamp >= fromDate && t.TransferTimeStamp <= toDate)
            .AsNoTracking();
        }

        public IQueryable<WagonTransfer> GetWagonTransfersArrivedFromMine(string fromMineId, DateTime fromDate, DateTime toDate, string arrivedLocationId)
        {
            var db = new CtsTransferContext<WagonTransfer>();
            var test = db.DbSet
            .Where(t => t.Equip.Location.ID == arrivedLocationId && t.IsValid == true)
            .Where(t => t.TransferTimeStamp >= fromDate && t.TransferTimeStamp <= toDate).ToArray();
            return db.DbSet
            .Where(t => t.Equip.Location.ID == arrivedLocationId && t.IsValid == true)
            .Where(t => t.FromDestID == fromMineId)
            //.Where(t => t.Direction == CTS_Core.ProjectConstants.WagonDirection_ToObject)
            .Where(t => t.TransferTimeStamp >= fromDate && t.TransferTimeStamp <= toDate)
            .AsNoTracking();
        }

        public IQueryable<WagonTransfer> GetWagonTransfersIncludeLocations(int? wagonScaleID, DateTime fromDate, DateTime toDate)
        {
            return cdb.WagonTransfers.Include(w => w.Equip.Location)
                .Where(s => wagonScaleID != null ? s.EquipID == wagonScaleID : true)
                .Where(d => d.TransferTimeStamp >= fromDate && d.TransferTimeStamp <= toDate)
                .Where(v => v.IsValid == true)
                .OrderByDescending(t => t.TransferTimeStamp)
                .AsNoTracking();
        }

        public IQueryable<WarehouseTransfer> GetWarehouseTransfers(int Id, DateTime fromDate, DateTime toDate)
        {
            return cdb.WarehouseTransfers
               .Where(s => s.WarehouseID == Id)
               .Where(d => d.TransferTimeStamp >= fromDate && d.TransferTimeStamp <= toDate)
               .OrderByDescending(t => t.TransferTimeStamp)
               .AsNoTracking();
        }

        public IQueryable<Location> GetAlllocaitons()
        {
            return cdb.Locations;
        }

        public int GetLocationIncome(string locationId, DateTime fromDate, DateTime toDate)
        {
            return (int)cdb.WagonTransfers
                .Where(t => t.TransferTimeStamp >= fromDate && t.TransferTimeStamp <= toDate)
                .Where(d => d.ToDest == locationId && d.IsValid == true)
                .Select(t => t.Brutto)
                .DefaultIfEmpty()
                .Sum();
        }

        public int GetLocationOutcome(string locationId, DateTime fromDate, DateTime toDate)
        {
            return (int)cdb.WagonTransfers
                .Where(t => t.TransferTimeStamp >= fromDate && t.TransferTimeStamp <= toDate)
                .Where(d => d.Equip.Location.ID == locationId && d.IsValid == true)
                .Select(t => t.Brutto)
                .DefaultIfEmpty()
                .Sum();
        }

        public int GetWagonScalesIdOnLocation(string locationId)
        {
            return cdb.WagonScales
                .Where(m => m.Location.ID == locationId)
                .FirstOrDefault()?
                .ID ?? 1;
        }

        public float GetBoilerConveyorProduction(int convId, DateTime fromDate, DateTime toDate)
        {
            var producationPerTimeInterval = cdb.InternalTransfers
                                    .Where(s => s.EquipID == convId)
                                    .Where(d => d.TransferTimeStamp >= fromDate && d.TransferTimeStamp <= toDate)
                                    .Where(v => v.IsValid == true).OrderByDescending(t => t.TransferTimeStamp)
                                    .Select(t => t.LotQuantity).DefaultIfEmpty(0);
            return producationPerTimeInterval.Sum().GetValueOrDefault();
        }

        public float GetConsumptionNorm(string mineId, DateTime fromDate, DateTime toDate)
        {
            var span = toDate - fromDate;
            var boiler = cdb.BoilerConsNorms.Find(mineId);
            if (boiler != null)
            {
                return (float)Math.Round((boiler.ConsNorm / 24 * span.TotalHours), 2);
            }
            return 0;
        }
    }
}