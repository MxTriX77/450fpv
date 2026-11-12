using System;
using System.IO;
using System.Linq;
using Fpv450.App;
using Fpv450.App.Session;
using Fpv450.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fpv450.Tests.App
{
    // w10-play-maps 1.1 (D-100): Play in place of Train, its carousel of House and Village, and what each card opens.
    public sealed class PlayTests
    {
        // The menu reads Play, Calibrate, Explore (review builds only) and Exit, and no item or hint names a map the item does
        // not open: Play's names both of Play's maps, Explore's the farmland's, Village, alone.
        [Test]
        public void TheMenuReadsPlayCalibrateExploreAndExit()
        {
            Assert.That(MenuScreen.Items(false).Select(i => i.Name), Is.EqualTo(new[] { "Play", "Calibrate", "Exit" }));
            Assert.That(MenuScreen.Items(true).Select(i => i.Name), Is.EqualTo(new[] { "Play", "Calibrate", "Explore", "Exit" }));
            var items = MenuScreen.Items(true);
            Assert.That(items.Select(i => i.Choice), Is.EqualTo(new[] { MenuChoice.Play, MenuChoice.Calibrate, MenuChoice.Explore, MenuChoice.Exit }));
            string play = items[0].Hint, explore = items[2].Hint;
            TestContext.WriteLine("Play: '" + play + "', Explore: '" + explore + "'");
            Assert.That(play, Does.Contain(PlayMaps.House.Name).And.Contain(PlayMaps.Village.Name));
            Assert.That(explore, Does.Contain(PlayMaps.Village.Name).And.Not.Contain(PlayMaps.House.Name));
            foreach (var i in items)
                Assert.That(i.Hint ?? "", Does.Not.Contain("test range").And.Not.Contain("Train").And.Not.Contain("farmland"), i.Name + "'s hint");
        }

        // Play's maps in the carousel's order: House, the test range from its houses' start (D-077), and Village, the farmland
        // from its village's start (D-092), each by its scene, which the build carries, and flown on its own world.
        [Test]
        public void PlayOffersHouseThenVillage()
        {
            Assert.That(PlayMaps.All, Is.EqualTo(new[] { PlayMaps.House, PlayMaps.Village }));
            Assert.That((PlayMaps.House.Id, PlayMaps.House.Name, PlayMaps.House.Scene), Is.EqualTo((FlightWorlds.TestRangeMap, "House", "TestRange")));
            Assert.That((PlayMaps.Village.Id, PlayMaps.Village.Name, PlayMaps.Village.Scene), Is.EqualTo((FlightWorlds.FarmlandMap, "Village", FarmlandExplore.Scene)));
            string[] built = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => Path.GetFileNameWithoutExtension(s.path)).ToArray();
            foreach (PlayMap map in PlayMaps.All)
            {
                Assert.That(map.Line, Is.Not.Empty, map.Name + "'s line");
                Assert.That(PlayMaps.ForScene(map.Scene), Is.SameAs(map), map.Name + " by its scene");
                Assert.That(built, Does.Contain(map.Scene), map.Name + "'s scene is in the build");
            }
            Assert.That(PlayMaps.ForScene(AppFlow.BootScene), Is.Null, "the menu's scene flies no map");
            Assert.That(PlayMaps.House.Starts(AppData.Root).Select(s => s.Name), Is.EqualTo(new[] { FlightWorlds.HousesStart }));
            Assert.That(PlayMaps.Village.Starts(AppData.Root).Select(s => s.Name), Is.EqualTo(new[] { FlightWorlds.FarmlandStart.Name }));
            FlightWorldChoice house = PlayMaps.House.World(AppData.Root);
            Assert.That(house.Id, Is.EqualTo(FlightWorlds.TestRangeMap), "House flies the test range");
            Assert.That(house.StartPoints.Select(s => s.Name), Is.EqualTo(new[] { FlightWorlds.HousesStart }));
        }

        // A card's still is task 1.2's render at unity/Generated/stills/<map id>.png (the player's copy under its
        // StreamingAssets' generated/stills/): read when there, and none (a plain card) when not.
        [Test]
        public void TheStillIsReadWhenTheBuildCarriesOne()
        {
            Assert.That(PlayMaps.StillPath(PlayMaps.House), Is.EqualTo(Path.Combine(AppData.Root, "unity", "Generated", "stills", "test-range.png")));
            Assert.That(PlayMaps.StillPath(PlayMaps.Village), Is.EqualTo(Path.Combine(AppData.Root, "unity", "Generated", "stills", "farmland.png")));
            string dir = Path.Combine(Path.GetTempPath(), "fpv450-stills-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var drawn = new Texture2D(64, 36, TextureFormat.RGBA32, false);
            Texture2D read = null;
            try
            {
                string path = Path.Combine(dir, "test-range.png");
                Assert.That(PlayMaps.LoadStill(path), Is.Null, "no file: a plain card");
                File.WriteAllBytes(path, drawn.EncodeToPNG());
                read = PlayMaps.LoadStill(path);
                Assert.That(read, Is.Not.Null);
                Assert.That((read.width, read.height), Is.EqualTo((64, 36)));
            }
            finally
            {
                Object.DestroyImmediate(drawn);
                if (read != null) Object.DestroyImmediate(read);
                Directory.Delete(dir, true);
            }
        }

        // The carousel stays within its cards; Show selects at once.
        [Test]
        public void TheCarouselStaysWithinItsCards()
        {
            var carousel = new MapCarousel { Cards = new[] { new MapCard { Name = "A" }, new MapCard { Name = "B" } } };
            Assert.That(carousel.Selected, Is.EqualTo(0));
            carousel.Move(-1);
            Assert.That(carousel.Selected, Is.EqualTo(0), "no card left of the first");
            carousel.Move(+1);
            carousel.Move(+1);
            Assert.That(carousel.Selected, Is.EqualTo(1), "none right of the last");
            carousel.Show(0);
            Assert.That(carousel.Selected, Is.EqualTo(0));
        }

        // The radio's sticks: roll past Push moves one card its way, pitch forward past Push chooses, each once until both
        // sticks are back near the centre; a stick held when the screen opens does nothing until it has been centred.
        [Test]
        public void TheSticksMoveOneCardAPushAndChooseForward()
        {
            var nav = new StickNav();
            Assert.That(nav.Step(0, 0.9, out bool choose), Is.EqualTo(0));
            Assert.That(choose, Is.False, "pitch held at the open chooses nothing");
            Assert.That(nav.Step(0.1, 0.1, out choose), Is.EqualTo(0), "centred");
            Assert.That(nav.Step(0.7, 0, out choose), Is.EqualTo(1), "roll right: the next card");
            Assert.That(nav.Step(0.95, 0, out choose), Is.EqualTo(0), "held: once");
            Assert.That(nav.Step(0.4, 0, out choose), Is.EqualTo(0), "not yet back");
            Assert.That(nav.Step(0.2, 0, out choose), Is.EqualTo(0), "back near the centre");
            Assert.That(nav.Step(-0.6, 0, out choose), Is.EqualTo(-1), "roll left: the card before");
            Assert.That(nav.Step(0, 0, out choose), Is.EqualTo(0));
            Assert.That(nav.Step(0, -0.9, out choose), Is.EqualTo(0));
            Assert.That(choose, Is.False, "pitch back chooses nothing");
            Assert.That(nav.Step(0, 0, out choose), Is.EqualTo(0));
            Assert.That(nav.Step(0, 0.65, out choose), Is.EqualTo(0));
            Assert.That(choose, Is.True, "pitch forward: chosen");
            Assert.That(nav.Step(0, 0.9, out choose), Is.EqualTo(0));
            Assert.That(choose, Is.False, "held: once");
            nav.Reset();
            Assert.That(nav.Step(0.9, 0, out choose), Is.EqualTo(0), "after a reset, a held stick waits for the centre");
        }

        // Play opens on the map it is given (AppFlow's: the one chosen last); with no key pressed and no radio, a frame moves
        // and chooses nothing. The keys themselves (← →, Enter, read as pressed this frame) are pressed by the player's smoke
        // test through PlayScreen's own read: in edit mode the Input System never answers wasPressedThisFrame for a manual
        // update (AppTests.Fly's and TestRangeTests' notes).
        [Test]
        public void PlayOpensOnTheMapChosenLast()
        {
            AppRadio.Unplug(); // no radio of the machine's moves the cards
            try
            {
                var play = new PlayScreen();
                foreach (PlayMap map in PlayMaps.All)
                {
                    play.Open(map);
                    Assert.That(play.Selected, Is.SameAs(map), "opens on " + map.Name);
                    Assert.That(play.Update(), Is.False, "no key and no radio: nothing chosen");
                    Assert.That(play.Selected, Is.SameAs(map), "nor moved");
                }
            }
            finally
            {
                AppRadio.Stop();
            }
        }
    }
}
