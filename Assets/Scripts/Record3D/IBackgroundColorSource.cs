using System.Threading.Tasks;

internal interface IBackgroundColorSource
{
    Task<byte[]> GetBackgroundColorBufferAsync(int idx);
}