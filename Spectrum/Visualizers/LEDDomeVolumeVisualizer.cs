﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spectrum.Base;
using Spectrum.LEDs;
using Spectrum.Audio;
using System.Diagnostics;

namespace Spectrum {

  class LEDDomeVolumeVisualizer : Visualizer {

    private readonly Configuration config;
    private readonly AudioInput audio;
    private readonly LEDDomeOutput dome;
    private int animationSize;
    private int centerOffset;

    private StrutLayout partLayout, indexLayout, sectionLayout;

    // For color-from-part mode, maps each part to a color
    private readonly int[] partColors = new int[4];
    // For color-from-index mode, maps each index to a color
    private readonly int[] indexColors = new int[6];
    // For color-from-part-and-spoke mode, maps each part/spoke to a color
    private readonly int[] partAndSpokeColors = new int[5];
    // For color-from-random mode, maps each strut to a color
    private readonly int[] randomStrutColors = new int[LEDDomeOutput.GetNumStruts()];
    private readonly Random random = new Random();
    private bool wipeStrutsNextCycle = false;

    public LEDDomeVolumeVisualizer(
      Configuration config,
      AudioInput audio,
      LEDDomeOutput dome
    ) {
      this.config = config;
      this.audio = audio;
      this.dome = dome;
      this.dome.RegisterVisualizer(this);
      this.animationSize = 0;
      this.centerOffset = 0;
      this.spokeLayout = this.BuildSpokeLayout();
      this.UpdateLayouts();
    }

    private readonly StrutLayout spokeLayout;
    private StrutLayout BuildSpokeLayout() {
      int[][] strutsBySpoke = new int[][] {
        new int[] {
          73, 81, 89, 97, 105, 74, 82, 90, 98, 106, 0, 2, 4, 6, 8, 10, 12, 14,
          16, 18,
        },
        new int[] {
          21, 26, 29, 34, 37, 71, 20, 111, 122, 27, 84, 125, 28, 87, 100, 35,
          136, 103, 36, 139,
        },
        new int[] {
          22, 25, 30, 33, 38, 76, 23, 115, 118, 24, 79, 92, 31, 129, 132, 32,
          95, 108, 39, 143,
        },
        new int[] {
          113, 120, 127, 134, 141, 185, 186, 187, 188, 189, 147, 171, 152, 174,
          157, 177, 162, 180, 183, 167, 146, 148, 55, 56, 151, 153, 57, 58, 156,
          158, 59, 60, 161, 163, 61, 62, 166, 168, 63, 64, 40, 41, 43, 44, 46,
          47, 49, 50, 52, 53, 170, 172, 173, 175, 176, 178, 179, 181, 182, 184,
        },
      };
      HashSet<int> reversedStruts = new HashSet<int>() {
        71, 73, 74, 22, 81, 82, 26, 90, 30, 89, 97, 98, 34, 38, 106, 105, 185,
        189, 188, 187, 186, 0, 20, 41, 115, 23, 2, 4, 79, 24, 44, 122, 27, 6, 8,
        87, 28, 47, 129, 31, 10, 12, 95, 32, 50, 136, 35, 14, 16, 103, 36, 53,
        143, 39, 18, 183, 184, 170, 171, 172, 173, 174, 175, 176, 177, 178, 179,
        180, 181, 182,
      };
      StrutLayoutSegment[] segments =
        new StrutLayoutSegment[strutsBySpoke.Length];
      for (int i = 0; i < strutsBySpoke.Length; i++) {
        segments[i] = new StrutLayoutSegment(new HashSet<Strut>(
          strutsBySpoke[i].Select(
            strut => reversedStruts.Contains(strut)
              ? Strut.ReversedFromIndex(this.config, strut)
              : Strut.FromIndex(this.config, strut)
          )
        ));
      }
      return new StrutLayout(segments);
    }

    private void UpdateLayouts() {
      int[] points = new int[] { 22, 26, 30, 34, 38, 70 };
      for (int i = 0; i < 5; i++) {
        points[i] += this.centerOffset;
      }
      if (points[4] >= 40) {
        points[4] -= 20;
      }
      StrutLayout[] layouts = StrutLayoutFactory.ConcentricFromStartingPoints(
        this.config,
        new HashSet<int>(points),
        4
      );
      this.partLayout = layouts[0];
      this.indexLayout = layouts[1];
      this.sectionLayout = layouts[2];
    }

    public int Priority {
      get {
        if (this.config.domeActiveVis != 0) {
          return 0;
        }
        return 2;
      }
    }

    private bool enabled = false;
    public bool Enabled {
      get {
        return this.enabled;
      }
      set {
        if (value == this.enabled) {
          return;
        }
        if (value) {
          this.wipeStrutsNextCycle = true;
        }
        this.enabled = value;
      }
    }

    public Input[] GetInputs() {
      return new Input[] { this.audio };
    }

    public void Visualize() {
      if (this.wipeStrutsNextCycle) {
        for (int i = 0; i < LEDDomeOutput.GetNumStruts(); i++) {
          Strut strut = Strut.FromIndex(this.config, i);
          for (int j = 0; j < strut.Length; j++) {
            this.dome.SetPixel(i, j, 0x000000);
          }
        }
        this.wipeStrutsNextCycle = false;
      }

      this.UpdateCenter();
      this.UpdateAnimationSize(this.config.domeVolumeAnimationSize);

      int subdivisions = this.partLayout.NumSegments / 2;
      int totalParts = this.config.domeVolumeAnimationSize;
      int volumeSplitInto = 2 * ((totalParts - 1) / 2 + 1);
      for (int part = 0; part < totalParts; part += 2) {
        var outwardSegment = this.partLayout.GetSegment(part);
        double startRange = (double)part / volumeSplitInto;
        double endRange = (double)(part + 2) / volumeSplitInto;
        double level = this.audio.Volume;
        double scaled = (level - startRange) /
          (endRange - startRange);
        scaled = Math.Max(Math.Min(scaled, 1.0), 0.0);
        startRange = Math.Min(startRange / level, 1.0);
        endRange = Math.Min(endRange / level, 1.0);

        foreach (Strut strut in outwardSegment.GetStruts()) {
          this.UpdateStrut(strut, scaled, startRange, endRange);
        }

        if (part + 1 == totalParts) {
          break;
        }

        for (int i = 0; i < 6; i++) {
          StrutLayoutSegment segment =
            this.sectionLayout.GetSegment(i + part * 3);
          double gradientStartPos = 0.0;
          double gradientStep = 1.0 / segment.GetStruts().Count;
          foreach (Strut strut in segment.GetStruts()) {
            double gradientEndPos = gradientStartPos + gradientStep;
            this.UpdateStrut(
              strut,
              scaled == 1.0 ? 1.0 : 0.0,
              gradientStartPos,
              gradientEndPos
            );
            gradientStartPos = gradientEndPos;
          }
        }
      }
      this.dome.Flush();
    }

    private void UpdateAnimationSize(int newAnimationSize) {
      if (newAnimationSize == this.animationSize) {
        return;
      }
      if (newAnimationSize > this.animationSize) {
        for (int i = this.animationSize; i < newAnimationSize; i++) {
          foreach (Strut strut in this.partLayout.GetSegment(i).GetStruts()) {
            this.dome.ReserveStrut(strut.Index);
          }
        }
        this.animationSize = newAnimationSize;
        return;
      }
      for (int i = this.animationSize - 1; i >= newAnimationSize; i--) {
        foreach (Strut strut in this.partLayout.GetSegment(i).GetStruts()) {
          for (int j = 0; j < strut.Length; j++) {
            this.dome.SetPixel(strut.Index, j, 0x000000);
          }
          this.dome.ReleaseStrut(strut.Index);
        }
      }
      this.animationSize = newAnimationSize;
    }

    private void UpdateCenter() {
      int newCenterOffset = (int)(
        this.config.beatBroadcaster.ProgressThroughBeat(
          this.config.domeVolumeRotationSpeed
        ) * 4);
      if (newCenterOffset == this.centerOffset) {
        return;
      }
      this.centerOffset = newCenterOffset;
      // Force an update of reserved struts
      this.UpdateAnimationSize(0);
      this.UpdateLayouts();
    }

    /**
     * percentageLit: what percentage of this strut should be lit?
     * startLitRange,endLitRange refer to the portion of the lit range this
     *   strut represents. if it's the first strut startLitRange is 0.0; f it's
     *   the last lit strut, then endLitRange is 1.0. keep in mind that the lit
     *   range is not the same as the whole range.
     */
    private void UpdateStrut(
      Strut strut,
      double percentageLit,
      double startLitRange,
      double endLitRange
    ) {
      double step = (endLitRange - startLitRange) / (strut.Length * percentageLit);
      for (int i = 0; i < strut.Length; i++) {
        double gradientPos =
          strut.GetGradientPos(percentageLit, startLitRange, endLitRange, i);
        int color;
        if (gradientPos != -1.0) {
          color = this.ColorFromPart(strut.Index, gradientPos);
          //color = this.ColorFromIndex(strut.Index, gradientPos);
          //color = this.ColorFromRandom(strut.Index);
          //color = this.ColorFromPartAndSpoke(strut.Index, gradientPos);
        } else {
          color = 0x000000;
        }
        this.dome.SetPixel(strut.Index, i, color);
      }
    }

    private int ColorFromIndex(int strut, double pixelPos) {
      int colorIndex;
      if (this.indexLayout.SegmentIndexOfStrutIndex(strut) == 0) {
        colorIndex = 1;
      } else if (this.indexLayout.SegmentIndexOfStrutIndex(strut) == 1) {
        colorIndex = 2;
      } else if (this.indexLayout.SegmentIndexOfStrutIndex(strut) == 2) {
        colorIndex = 3;
      } else if (this.indexLayout.SegmentIndexOfStrutIndex(strut) == 3) {
        colorIndex = 4;
      } else if (this.indexLayout.SegmentIndexOfStrutIndex(strut) == 4) {
        colorIndex = 5;
      } else if (this.indexLayout.SegmentIndexOfStrutIndex(strut) == 5) {
        colorIndex = 0;
      } else {
        throw new Exception("unsupported value");
      }
      return this.dome.GetGradientColor(
        colorIndex,
        pixelPos,
        this.config.beatBroadcaster.ProgressThroughBeat(
          this.config.domeGradientSpeed
        ),
        true
      );
    }

    private int ColorFromPart(int strut, double pixelPos) {
      int colorIndex;
      if (this.partLayout.SegmentIndexOfStrutIndex(strut) == 0) {
        colorIndex = 1;
      } else if (this.partLayout.SegmentIndexOfStrutIndex(strut) == 1) {
        colorIndex = 2;
      } else if (this.partLayout.SegmentIndexOfStrutIndex(strut) == 2) {
        colorIndex = 3;
      } else if (this.partLayout.SegmentIndexOfStrutIndex(strut) == 3) {
        colorIndex = 0;
      } else {
        throw new Exception("unsupported value");
      }
      return this.dome.GetGradientColor(
        colorIndex,
        pixelPos,
        this.config.beatBroadcaster.ProgressThroughBeat(
          this.config.domeGradientSpeed
        ),
        true
      );
    }

    private int ColorFromRandom(int strut) {
      int color = this.randomStrutColors[strut];
      if (color == 0) {
        color = this.RandomColor();
        this.randomStrutColors[strut] = color;
      }
      return color;
    }

    private int RandomColor() {
      int brightnessByte = (int)(
        0xFF * this.config.domeMaxBrightness *
        this.config.domeBrightness
      );
      int color = 0;
      for (int i = 0; i < 3; i++) {
        color |= (int)(random.NextDouble() * brightnessByte) << (i * 8);
      }
      return color;
    }

    private int ColorFromPartAndSpoke(int strut, double pixelPos) {
      int colorIndex;
      if (this.partLayout.SegmentIndexOfStrutIndex(strut) == 1) {
        colorIndex = 1;
      } else if (this.partLayout.SegmentIndexOfStrutIndex(strut) == 3) {
        colorIndex = 2;
      } else if (spokeLayout.SegmentIndexOfStrutIndex(strut) == 0) {
        colorIndex = 3;
      } else if (spokeLayout.SegmentIndexOfStrutIndex(strut) == 1) {
        colorIndex = 4;
      } else if (spokeLayout.SegmentIndexOfStrutIndex(strut) == 2) {
        colorIndex = 5;
      } else if (spokeLayout.SegmentIndexOfStrutIndex(strut) == 3) {
        colorIndex = 0;
      } else {
        throw new Exception("unsupported value");
      }
      return this.dome.GetGradientColor(
        colorIndex,
        pixelPos,
        this.config.beatBroadcaster.ProgressThroughBeat(
          this.config.domeGradientSpeed
        ),
        true
      );
    }

  }

}
