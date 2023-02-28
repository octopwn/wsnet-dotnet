using System.Threading.Tasks;

namespace WSNet
{
    class Transport
    {
        public bool isOpen { get; set; }
        virtual public async Task Send(byte[] data)
        {
        }
    }

    
}
