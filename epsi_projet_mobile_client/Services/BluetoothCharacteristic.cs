using Plugin.BLE.Abstractions.Contracts;

namespace epsi_projet_mobile_client.Services;

public class BluetoothCharacteristic(ICharacteristic characteristic)
{
    private readonly List<byte> _readBuffer = [];
    private readonly List<byte> _writeBuffer = [];

    public async Task Flush()
    {
        await characteristic.WriteAsync(_writeBuffer.ToArray());
        _writeBuffer.Clear();
    }

    public async Task<int> ReadAsync(byte[] buffer)
    {
        var count = buffer.Length;
        var offset = 0;
        
        while (count != 0)
        {
            if (_readBuffer.Count != 0)
            {
                var readCount = Math.Min(_readBuffer.Count, count);
                _readBuffer.CopyTo(0, buffer, offset, readCount);
                _readBuffer.RemoveRange(0, readCount);

                offset += readCount;
                count -= readCount;
            }

            if (count == 0) break;
            
            var (bytes, resultCode) = await characteristic.ReadAsync();

            if (resultCode == 0)
            {
                _readBuffer.AddRange(bytes);
            }
            else
            {
                throw new Exception("Failed to read characteristic");
            }
        }
        
        return buffer.Length;
    }

    public void Write(IEnumerable<byte> buffer)
    {
        _writeBuffer.AddRange(buffer);
    }

}