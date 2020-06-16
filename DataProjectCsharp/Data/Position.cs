﻿using DataProjectCsharp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;

namespace DataProjectCsharp.Data
{

    public class PositionSnapshot
    {
        public DateTime date;
        public long quantity;
        public decimal averageCost;
        public PositionSnapshot(DateTime date, long quantity, decimal averageCost)
        {
            this.date = date;
            this.quantity = quantity;
            this.averageCost = averageCost;
        }
    }

    public class OpenLots
    {
        public DateTime date;
        public long quantity;
        public decimal price;
        public OpenLots(DateTime date, long quantity, decimal price)
        {
            this.date = date;
            this.quantity = quantity;
            this.price = price;
        }

        public decimal GetTradeValue()
        {
            return this.quantity * this.price;
        }
    }

    public class Position
    {
        // this object is comprised of a list of transactions. I can update the position object with transactions. Position.AddTransaction(Transaction)
        // current limitation transactions will need to be added by the correct date order for this to work.
        public readonly string symbol;
        public decimal averageCost { get; set; }
        public long netQuantity { get; set; }
        private bool isLong { get; set; }

        protected List<PositionSnapshot> positionBreakdown;
        private Queue<OpenLots> openLots;

        public Position(string symbol)
        {
            this.symbol = symbol;
            this.averageCost = 0;
            this.netQuantity = 0;
            this.positionBreakdown = new List<PositionSnapshot>();
            this.openLots = new Queue<OpenLots>();
        }


        public void AddTransaction(Trade transaction)
        {
            if (transaction.Ticker != this.symbol)
            {
                throw new InvalidOperationException("The transaction ticker does not match the ticker of this position");
            }

            OpenLots lot = new OpenLots(transaction.TradeDate, transaction.Quantity, transaction.Price);

            // make sure a transaction cannot equal zero in my models
            // position has no trades
            // used to be (transaction.Quantity * netQuantity == 0) but if the net quantity is zero then this will always be true
            // and i need to ignore trades where the transaction.quantity is zero;
            if (netQuantity == 0)
            {
                this.isLong = (transaction.Quantity >= 0);
            }

            //trades in diff direction to position
            else if (transaction.Quantity * netQuantity < 0)
            {
                while (openLots.Count > 0 && transaction.Quantity != 0)
                {
                    if(Math.Abs(transaction.Quantity) >= Math.Abs(openLots.Peek().quantity))
                    {
                        transaction.Quantity += openLots.Peek().quantity;
                        openLots.Dequeue();
                    }
                    else
                    {
                        openLots.Peek().quantity += transaction.Quantity;
                        transaction.Quantity = 0;
                    }
                }
                if (transaction.Quantity != 0)
                {
                    lot.quantity = transaction.Quantity;
                }

            }
            if (transaction.Quantity != 0)
            {
                openLots.Enqueue(lot);
            }

            UpdatePosition(transaction);
            // i need to update position regardless, 
            // i need update the closed lots regardless(give it another name like trade summary), 
            // if transaction.Quantity!=0 need to push the lots)

            //when all said and done, net position should equal total open lots
        }

        public decimal GetTotalMarketValue()
        {
            decimal result = 0;
            if (this.openLots.Count > 0)
            {
                foreach (OpenLots lot in this.openLots)
                {
                    result += lot.GetTradeValue();
                }
            }
            return result;
        }
        public List<PositionSnapshot> GetBreakdown()
        {
            return this.positionBreakdown;
        }

        private void UpdatePosition(Trade transaction)
        {
            this.netQuantity = this.openLots.Sum(lots => lots.quantity);
            if (transaction.Quantity==0 && this.openLots.Count > 0)
            {
                this.averageCost = this.openLots.Peek().price;
            }
            else
            {
                this.averageCost = GetAverageCost();
            }            
            CheckDirection();
            AppendBreakdown(transaction.TradeDate);
        }

        private void AppendBreakdown(DateTime tradeDate)
        {
            PositionSnapshot snapshot = new PositionSnapshot(tradeDate, this.netQuantity, this.averageCost);
            // if theres no trades add this trade
            if (positionBreakdown.Count == 0)
            {
                this.positionBreakdown.Add(snapshot);
            }
            else
            {
                //if theres a trade that took place on the same day then override it;
                int lastIndex = this.positionBreakdown.Count - 1;
                DateTime lastTradeDate = this.positionBreakdown[lastIndex].date;
                if(tradeDate == lastTradeDate)
                {
                    this.positionBreakdown[lastIndex] = snapshot;
                }
                else
                {
                    this.positionBreakdown.Add(snapshot);
                }
            }
            
        }

        private decimal GetAverageCost()
        {
            decimal marketValue = GetTotalMarketValue();

            return marketValue/this.netQuantity;
        }

        private void CheckDirection()
        {
            if (this.netQuantity < 0 && this.isLong)
            {
                this.isLong = false;
            }
            else if (this.netQuantity > 0 && !this.isLong)
            {
                this.isLong = true;
            }
        }
    }
}
