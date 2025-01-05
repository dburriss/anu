using System.Threading.Tasks;

namespace Ez;

public interface IJob
{
    Task Execute(IJobContext context);
    Task Compensate(IJobContext context);
}
