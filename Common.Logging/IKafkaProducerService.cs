using System;
using System.Threading.Tasks;

namespace Common.Logging
{
    public interface IKafkaProducerService
    {
        /// <summary>
        /// Kafka veya Elasticsearch için mesaj üretimi/loglama işlemini gerçekleştirir.
        /// </summary>
        /// <param name="mode">"log" veya "event" seçimi yapılır. "log" Elasticsearch, "event" Kafka için kullanılır.</param>
        /// <param name="topicOrIndex">Kafka topic adı veya Elasticsearch index adı.</param>
        /// <param name="key">Kafka için mesaj anahtarı (log işlemleri için null olabilir).</param>
        /// <param name="value">Mesaj içeriği.</param>
        /// <param name="cancellationToken">Operasyonu iptal etmek için kullanılan token.</param>
        Task ProcessMessageAsync(string mode, string topicOrIndex, string key, string value, CancellationToken cancellationToken);
    }

}
