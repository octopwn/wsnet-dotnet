using System.Threading.Tasks;
using WSNet.Protocol;

namespace WSNet.Modules.SocketComm
{
    abstract class SocketSession
    {
        public CMDConnect initiator_cmd;
        public CMDHeader initiator_cmdhdr;


        public abstract Task<bool> send(CMDHeader cmdhdr);
        public abstract void stop();
    }

    abstract class SocketServerSession
    {
        public CMDConnect initiator_cmd;
        public CMDHeader initiator_cmdhdr;


        public abstract Task<bool> send(CMDHeader cmdhdr);
        public abstract void stop();
    }

}
