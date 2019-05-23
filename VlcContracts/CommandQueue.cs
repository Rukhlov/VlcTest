using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VlcContracts
{
    public class InternalCommand
    {
        public string command = "";
        public object[] args = null;
    }


    public class CommandQueue
    {
        private readonly LinkedList<InternalCommand> list = new LinkedList<InternalCommand>();

        private readonly Dictionary<string, LinkedListNode<InternalCommand>> dict = new Dictionary<string, LinkedListNode<InternalCommand>>();

        private readonly object locker = new object();

        public InternalCommand Dequeue()
        {
            lock (locker)
            {
                InternalCommand command = null;
                if (list.Count > 0)
                {
                    command = list.First();
                    list.RemoveFirst();

                    var key = command.command;
                    if (dict.ContainsKey(key))
                    {
                        dict.Remove(key);
                    }
                }
                return command;
            }
        }

        public void Enqueue(InternalCommand command)
        {
            lock (locker)
            {
                //if(list.Count> maxCount)
                //{
                //    //...
                //}
                var key = command.command;
                if (dict.ContainsKey(key))
                {
                    var node = dict[key];
                    node.Value = command;
                }
                else
                {
                    LinkedListNode<InternalCommand> node = list.AddLast(command);
                    dict.Add(key, node);
                }

            }
        }

        public void Clear()
        {
            lock (locker)
            {
                list.Clear();
                dict.Clear();
            }
        }
    }
}
