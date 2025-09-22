using Domain.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Domain.Interfaces
{
    public interface IRealTimeDetectionService : IDisposable
    {
        /// <summary>
        /// Событие обнаружения человека
        /// </summary>
        event EventHandler<HumanDetectionResult> OnPersonDetected;

        /// <summary>
        /// Событие ошибки детекции
        /// </summary>
        event EventHandler<string> OnDetectionError;

        /// <summary>
        /// Событие изменения статуса
        /// </summary>
        event EventHandler<string> OnStatusChanged;

        /// <summary>
        /// Запуск реального времени
        /// </summary>
        Task StartRealTimeDetectionAsync(CancellationToken ct = default);

        /// <summary>
        /// Остановка реального времени
        /// </summary>
        Task StopRealTimeDetectionAsync();

        /// <summary>
        /// Флаг активности детекции
        /// </summary>
        bool IsDetecting { get; }

        /// <summary>
        /// Текущий FPS
        /// </summary>
        double CurrentFps { get; }

        /// <summary>
        /// Установка целевого FPS
        /// </summary>
        int TargetFps { get; set; }

        /// <summary>
        /// Последний результат детекции
        /// </summary>
        HumanDetectionResult LastDetectionResult { get; }
    }
}