using Client;
using Microsoft.Extensions.Time.Testing;

namespace Canelary.Tests
{
    public class WorkerTimingTests
    {
        [Fact]
        public void TimeUntilNext_BeforeTargetHour_ReturnsRemainderOfDayPlusTarget()
        {
            // Lunes 10:00, target 02:00 -> proxima ocurrencia: martes 02:00. Diferencia: 16hs.
            var fake = new FakeTimeProvider(new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.Zero));
            fake.SetLocalTimeZone(TimeZoneInfo.Utc);

            TimeSpan remaining = Worker.TimeUntilNext(fake, hourOfDay: 2);

            Assert.Equal(TimeSpan.FromHours(16), remaining);
        }

        [Fact]
        public void TimeUntilNext_ExactlyAtMidnight_ReturnsFullDayPlusTarget()
        {
            // 00:00 con target 02:00 -> proxima ocurrencia: mañana 02:00 (no hoy 02:00,
            // porque el calculo usa AddDays(1) explicitamente para evitar dispararse dos veces).
            var fake = new FakeTimeProvider(new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero));
            fake.SetLocalTimeZone(TimeZoneInfo.Utc);

            TimeSpan remaining = Worker.TimeUntilNext(fake, hourOfDay: 2);

            Assert.Equal(TimeSpan.FromHours(26), remaining);
        }

        [Fact]
        public void TimeUntilNext_LateInDayTargetingZero_ReturnsTimeUntilUpcomingMidnight()
        {
            // 23:30 con target 0 -> proxima medianoche = 30 minutos.
            // El calculo siempre suma AddDays(1) primero y luego AddHours(target), asi que
            // target=0 a las 23:30 es equivalente a "manana 00:00" = ahora + 30 min.
            var fake = new FakeTimeProvider(new DateTimeOffset(2026, 1, 5, 23, 30, 0, TimeSpan.Zero));
            fake.SetLocalTimeZone(TimeZoneInfo.Utc);

            TimeSpan remaining = Worker.TimeUntilNext(fake, hourOfDay: 0);

            Assert.Equal(TimeSpan.FromMinutes(30), remaining);
        }

        [Fact]
        public void TimeUntilNext_NegativeResult_FallsBackToOneMinute()
        {
            // hourOfDay negativo es valor invalido pero no deberia colgar el loop.
            // El fallback de 1 minuto garantiza progreso.
            var fake = new FakeTimeProvider(new DateTimeOffset(2026, 1, 5, 23, 59, 0, TimeSpan.Zero));
            fake.SetLocalTimeZone(TimeZoneInfo.Utc);

            TimeSpan remaining = Worker.TimeUntilNext(fake, hourOfDay: -25);

            Assert.Equal(TimeSpan.FromMinutes(1), remaining);
        }
    }
}
