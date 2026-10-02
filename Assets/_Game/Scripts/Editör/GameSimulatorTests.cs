using System;
using UnityEditor;
using UnityEngine;

public static class GameSimulatorTests
{
    [MenuItem("Tools/ColorLoop/Tests/Banda Gönderme Kuralları")]
    private static void Run()
    {
        try
        {
            TestQueueOrder();
            TestEntryBlock();
            TestCapacity();
            TestCloneIsolation();

            Debug.Log(
                "GameSimulator testleri geçti: " +
                "sıra, giriş mesafesi, kapasite ve kopya bağımsızlığı.");

            EditorUtility.DisplayDialog(
                "Testler geçti",
                "Banda gönderme kontrolleri başarılı.",
                "Tamam");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Test başarısız",
                exception.Message,
                "Tamam");
        }
    }

    private static SimulationState CreateState(int capacity)
    {
        BoardState board = new BoardState(
            2,
            2,
            new int[]
            {
                3, 0,
                3, 0
            });

        ShooterState[] shooters =
        {
            new ShooterState(3, 2),
            new ShooterState(0, 2)
        };

        return new SimulationState(
            board,
            shooters,
            conveyorCapacity: capacity,
            waitingSlotCount: 2);
    }

    private static GameSimulator CreateSimulator(SimulationState state)
    {
        return new GameSimulator(
            state,
            cellSize: 0.15,
            distanceFromBoard: 0.7,
            entryClearance: 0.6);
    }

    private static void TestQueueOrder()
    {
        SimulationState state = CreateState(2);
        GameSimulator simulator = CreateSimulator(state);

        Require(
            simulator.TryLaunch(1) == LaunchResult.NotQueueFront,
            "Sıranın arkasındaki shooter gönderilebildi.");

        Require(
            state.NextQueueIndex == 0 &&
            state.ActiveShooterCount == 0 &&
            state.NextEntryOrder == 0 &&
            state.GetShooter(1).Location == ShooterLocation.Queue,
            "Reddedilen hamle oyun durumunu değiştirdi.");

        Require(
            simulator.TryLaunch(0) == LaunchResult.Success,
            "Sıranın önündeki shooter gönderilemedi.");

        Require(
            state.NextQueueIndex == 1 &&
            state.ActiveShooterCount == 1 &&
            state.GetShooter(0).Location == ShooterLocation.Conveyor &&
            state.GetShooter(0).EntryOrder == 0,
            "Başarılı gönderme durumu yanlış.");

        Require(
            simulator.TryLaunch(0) == LaunchResult.AlreadyOnConveyor,
            "Banttaki shooter tekrar gönderilebildi.");

        Require(
            state.ActiveShooterCount == 1 &&
            state.NextQueueIndex == 1 &&
            state.NextEntryOrder == 1,
            "Tekrar gönderme denemesi durumu değiştirdi.");
    }

    private static void TestEntryBlock()
    {
        SimulationState state = CreateState(2);
        GameSimulator simulator = CreateSimulator(state);

        Require(
            simulator.TryLaunch(0) == LaunchResult.Success,
            "İlk shooter gönderilemedi.");

        Require(
            simulator.TryLaunch(1) == LaunchResult.EntryBlocked,
            "Giriş doluyken ikinci shooter gönderilebildi.");

        Require(
            state.NextQueueIndex == 1 &&
            state.ActiveShooterCount == 1 &&
            state.GetShooter(1).Location == ShooterLocation.Queue,
            "Giriş engeli sırasında sıra bozuldu.");
    }

    private static void TestCapacity()
    {
        SimulationState state = CreateState(1);
        GameSimulator simulator = CreateSimulator(state);

        Require(
            simulator.TryLaunch(0) == LaunchResult.Success,
            "Kapasite testinde ilk gönderme başarısız.");

        Require(
            simulator.TryLaunch(1) == LaunchResult.ConveyorFull,
            "Dolu banda shooter kabul edildi.");

        Require(
            state.ActiveShooterCount == 1 &&
            state.NextQueueIndex == 1,
            "Kapasite sınırı korunamadı.");
    }

    private static void TestCloneIsolation()
    {
        SimulationState original = CreateState(2);
        GameSimulator simulator = CreateSimulator(original);

        Require(
            simulator.TryLaunch(0) == LaunchResult.Success,
            "Kopya testi için shooter gönderilemedi.");

        SimulationState copy = original.Clone();

        Require(
            copy.GetShooter(0).Location == ShooterLocation.Conveyor &&
            copy.GetShooter(0).EntryOrder ==
                original.GetShooter(0).EntryOrder &&
            copy.NextQueueIndex == original.NextQueueIndex,
            "Banttaki shooter durumu kopyalanamadı.");

        Require(
            copy.GetShooter(0).State.TryConsumeAmmo(),
            "Kopyada mühimmat harcanamadı.");

        Require(
            original.GetShooter(0).State.RemainingAmmo == 2 &&
            copy.GetShooter(0).State.RemainingAmmo == 1,
            "Kopyanın mühimmat değişikliği asıl duruma sızdı.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}