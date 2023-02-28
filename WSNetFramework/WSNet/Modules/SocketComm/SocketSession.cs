using System.Threading.Tasks;

namespace WSNet.SocketComm
{
    abstract class SocketSession
    {
        public CMDConnect initiator_cmd;
        public CMDHeader initiator_cmdhdr;


        public abstract Task<bool> send(CMDHeader cmdhdr);
        public abstract void stop();
    }



}
