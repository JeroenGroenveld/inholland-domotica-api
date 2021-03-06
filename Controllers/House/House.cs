﻿using System;
using System.Net.Sockets;
using System.Collections.Generic;

namespace Domotica_API.Controllers.House
{
    public class Heater
    {
        public int id { get; set; }
        public double temperature { get; set; }
    }

    public class Item
    {
        public int id { get; set; }
        public string type { get; set; }
        public bool status { get; set; }
        public int floor { get; set; }
        public string location { get; set; }
    }

    public class House
    {
        public TcpClient client;
        public NetworkStream stream;

        public const int HEATER_ID = 1;
        public const  int HEATER_MAX = 35;
        public const int HEATER_MIN = 12;
        public const string HEATER_CMD_NAME = "heater";

        public const string LAMP_CMD_NAME = "lamp";
        public const string LAMP_CMD_LIST_NAME = "lamps";

        public const string WINDOW_CMD_NAME = "window";
        public const string WINDOW_CMD_LIST_NAME = "windows";

        public bool Connect()
        {
            this.client = new TcpClient();
            IAsyncResult connection = this.client.BeginConnect(Config.House.ADDRESS, Config.House.PORT, null, null);
            //Set timeout to 2 seconds.
            bool success = connection.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));

            if (success == false || client.Connected == false)
            {
                return false;
            }

            // Get a client stream for reading and writing.
            this.stream = this.client.GetStream();
            return true;
        }

        private string GetResponse(String message)
        {
            message += Environment.NewLine;

            this.Send(message);

            return this.Read().Trim();
        }

        private string Read()
        {
            // Buffer to store the response bytes.
            byte[] dataBuffer = new byte[client.ReceiveBufferSize];

            // Read the first batch of the TcpServer response bytes.
            int BytesRead = this.stream.Read(dataBuffer, 0, dataBuffer.Length);

            // Recived bytes to string
            return System.Text.Encoding.ASCII.GetString(dataBuffer, 0, BytesRead);
        }

        private void Send(string message)
        {
            // Translate the passed message into ASCII and store it as a Byte array.
            byte[] dataBuffer = System.Text.Encoding.ASCII.GetBytes(message);

            // Send the message to the connected TcpServer. 
            this.stream.Write(dataBuffer, 0, dataBuffer.Length);
        }

        public void Close()
        {
            // Close everything.
            byte[] dataBuffer = System.Text.Encoding.ASCII.GetBytes("exit" + Environment.NewLine);
            this.stream.Write(dataBuffer, 0, dataBuffer.Length);

            // Buffer to store the response bytes.
            dataBuffer = new byte[client.ReceiveBufferSize];
            this.stream.Read(dataBuffer, 0, dataBuffer.Length);

            this.stream.Close();
            this.client.Close();
        }

        public void SetHeaterTemperature(double temperature)
        {
            this.GetResponse(HEATER_CMD_NAME + " " + temperature);
        }

        public double GetHeaterTemperature()
        {
            string Status = this.GetResponse(HEATER_CMD_NAME);
            return double.Parse(Status.Split(' ')[Status.Split(' ').Length - 1]);
        }

        public Item[] GetList(string cmd_name_item, string cmd_name_list)
        {
            //Number of items
            int numberOfItems = int.Parse(this.GetResponse(cmd_name_list));

            // Create array for lamps
            Item[] Items = new Item[numberOfItems];

            // Ask status foreach lamp
            for (int index = 0; index < Items.Length; index++)
            {
                Items[index] = this.GetItemInformation(index, cmd_name_item);
            }

            return Items;
        }

        public Item GetItemInformation(int _id, string cmd_name_item)
        {
            bool Status = false;

            if (cmd_name_item == LAMP_CMD_NAME)
            {
                Status = this.GetResponse(cmd_name_item + " " + _id).Contains("On");
            }

            if (cmd_name_item == WINDOW_CMD_NAME)
            {
                Status = this.GetResponse(cmd_name_item + " " + _id).Contains("Open");
            }

            string Description = this.GetResponse("whereis " + cmd_name_item + " " + _id);
            int Floor = int.Parse(Description.Split('@')[1].Split(' ')[1]);
            string Location = Description.Split('@')[1].Split(' ')[2];

            return new Item { id = _id, type = cmd_name_item, status = Status, floor = Floor, location = Location };
        }

        public Item SetItem(int id, string cmd_name_item, string cmd_name_list, bool Switch, string NewStatus = "")
        {
            int numberOfLamps = int.Parse(this.GetResponse(cmd_name_list));
            if (id < 0 || id > numberOfLamps - 1)
            {
                return null;
            }
           
            if (Switch)
            {
                string ItemInfo = this.GetResponse(cmd_name_item + " " + id);
                if (ItemInfo.Contains("On"))
                {
                    NewStatus = "off";
                } else if (ItemInfo.Contains("Off"))
                {
                    NewStatus = "on";
                } else if (ItemInfo.Contains("Open"))
                {
                    NewStatus = "close";
                } else if (ItemInfo.Contains("Close"))
                {
                    NewStatus = "open";
                }
            }

            this.GetResponse(cmd_name_item + " " + id + " " + NewStatus);

            Item result = this.GetItemInformation(id, cmd_name_item);

            //Switch the status of the window result. Reason:
            //When we close the window the close command will be send.
            //After that we get the new information for the item(Window). And it will return that the window is still open
            //This is because the window thing is still animating. When it's done animation(When the window is closed) the status will be updated.
            if (cmd_name_item == WINDOW_CMD_NAME)
            {
                result.status = !result.status;
            }

            return result;
        }
    }
}
