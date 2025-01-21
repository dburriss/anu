using System.Threading.Tasks;

namespace Ez.Jobs;

public interface IJob
{
    Task Execute(IJobContext context);
    Task Compensate(IJobContext context);
}
