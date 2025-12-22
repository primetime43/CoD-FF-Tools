using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using Call_of_Duty_FastFile_Editor.Models;

namespace Call_of_Duty_FastFile_Editor.Export
{
    /// <summary>
    /// Exports animation data to BVH (Biovision Hierarchy) format.
    /// BVH is a simple text-based motion capture format with native Blender support.
    /// </summary>
    public class BVHWriter
    {
        private readonly StringBuilder _builder;
        private readonly XAnimParts _xanim;
        private readonly XAnimExtractedData? _extractedData;
        private readonly float _frameTime;

        /// <summary>
        /// Creates a new BVHWriter for the given animation.
        /// </summary>
        /// <param name="xanim">The XAnimParts to export.</param>
        /// <param name="extractedData">Optional extracted keyframe data.</param>
        public BVHWriter(XAnimParts xanim, XAnimExtractedData? extractedData = null)
        {
            _xanim = xanim ?? throw new ArgumentNullException(nameof(xanim));
            _extractedData = extractedData;
            _builder = new StringBuilder();

            // Calculate frame time (seconds per frame)
            float fps = float.IsNaN(xanim.Framerate) || xanim.Framerate <= 0 ? 30.0f : xanim.Framerate;
            _frameTime = 1.0f / fps;
        }

        /// <summary>
        /// Generates the BVH file content.
        /// </summary>
        /// <returns>The complete BVH file as a string.</returns>
        public string Generate()
        {
            _builder.Clear();

            // Write hierarchy section
            WriteHierarchy();

            // Write motion section
            WriteMotion();

            return _builder.ToString();
        }

        /// <summary>
        /// Writes the HIERARCHY section defining the skeleton structure.
        /// Uses CoD bone naming conventions to build a proper hierarchy.
        /// </summary>
        private void WriteHierarchy()
        {
            _builder.AppendLine("HIERARCHY");

            var boneNames = _xanim.BoneNames;
            if (boneNames.Count == 0)
            {
                // No bones - create a simple root
                WriteSingleRootBone("root");
                return;
            }

            // Build hierarchy tree based on CoD bone naming conventions
            var hierarchy = BuildBoneHierarchy(boneNames);

            // Find the root bone
            var rootBone = hierarchy.FirstOrDefault(b => b.Parent == null);
            if (rootBone == null)
            {
                WriteSingleRootBone(boneNames[0]);
                return;
            }

            // Write the hierarchy recursively
            WriteHierarchyRecursive(rootBone, 0, true);
        }

        /// <summary>
        /// Builds a bone hierarchy based on CoD naming conventions.
        /// </summary>
        private List<BoneNode> BuildBoneHierarchy(List<string> boneNames)
        {
            var nodes = boneNames.Select((name, idx) => new BoneNode
            {
                Name = name,
                Index = idx,
                Offset = GetBoneOffset(name)
            }).ToList();

            // Define parent-child relationships based on CoD conventions
            var parentMapping = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                // Spine chain
                { "j_mainroot", new[] { "tag_origin", "root" } },
                { "j_hip", new[] { "j_mainroot", "tag_origin", "root" } },
                { "j_spinelower", new[] { "j_hip", "j_mainroot" } },
                { "j_spineupper", new[] { "j_spinelower", "j_hip" } },
                { "j_spine4", new[] { "j_spineupper", "j_spinelower" } },

                // Head chain
                { "j_neck", new[] { "j_spine4", "j_spineupper" } },
                { "j_head", new[] { "j_neck", "j_spine4" } },

                // Left arm
                { "j_clavicle_le", new[] { "j_spine4", "j_spineupper" } },
                { "j_shoulder_le", new[] { "j_clavicle_le", "j_spine4" } },
                { "j_elbow_le", new[] { "j_shoulder_le" } },
                { "j_wrist_le", new[] { "j_elbow_le" } },
                { "j_wristtwist_le", new[] { "j_wrist_le" } },

                // Right arm
                { "j_clavicle_ri", new[] { "j_spine4", "j_spineupper" } },
                { "j_shoulder_ri", new[] { "j_clavicle_ri", "j_spine4" } },
                { "j_elbow_ri", new[] { "j_shoulder_ri" } },
                { "j_wrist_ri", new[] { "j_elbow_ri" } },
                { "j_wristtwist_ri", new[] { "j_wrist_ri" } },

                // Left leg
                { "j_hip_le", new[] { "j_hip", "j_mainroot" } },
                { "j_knee_le", new[] { "j_hip_le" } },
                { "j_ankle_le", new[] { "j_knee_le" } },
                { "j_ball_le", new[] { "j_ankle_le" } },

                // Right leg
                { "j_hip_ri", new[] { "j_hip", "j_mainroot" } },
                { "j_knee_ri", new[] { "j_hip_ri" } },
                { "j_ankle_ri", new[] { "j_knee_ri" } },
                { "j_ball_ri", new[] { "j_ankle_ri" } },

                // Weapon/gun bones
                { "tag_weapon", new[] { "j_wrist_ri", "j_gun" } },
                { "tag_weapon_right", new[] { "j_wrist_ri" } },
                { "tag_weapon_left", new[] { "j_wrist_le" } },
                { "j_gun", new[] { "j_wrist_ri", "tag_weapon" } },
            };

            // Assign parents
            foreach (var node in nodes)
            {
                if (parentMapping.TryGetValue(node.Name, out var possibleParents))
                {
                    foreach (var parentName in possibleParents)
                    {
                        var parent = nodes.FirstOrDefault(n =>
                            n.Name.Equals(parentName, StringComparison.OrdinalIgnoreCase));
                        if (parent != null)
                        {
                            node.Parent = parent;
                            parent.Children.Add(node);
                            break;
                        }
                    }
                }
            }

            // For bones without assigned parents, try to find parent by prefix
            foreach (var node in nodes.Where(n => n.Parent == null))
            {
                var parent = FindParentByNaming(node.Name, nodes);
                if (parent != null && parent != node)
                {
                    node.Parent = parent;
                    parent.Children.Add(node);
                }
            }

            // Any remaining orphans become children of root
            var rootCandidates = new[] { "tag_origin", "j_mainroot", "j_main", "root" };
            BoneNode? rootNode = null;
            foreach (var candidate in rootCandidates)
            {
                rootNode = nodes.FirstOrDefault(n =>
                    n.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase));
                if (rootNode != null) break;
            }

            if (rootNode == null)
                rootNode = nodes.FirstOrDefault(n => n.Parent == null) ?? nodes[0];

            // Attach orphans to root
            foreach (var node in nodes.Where(n => n.Parent == null && n != rootNode))
            {
                node.Parent = rootNode;
                rootNode.Children.Add(node);
            }

            // Mark root as having no parent
            rootNode.Parent = null;

            return nodes;
        }

        /// <summary>
        /// Tries to find a parent bone based on naming patterns.
        /// </summary>
        private BoneNode? FindParentByNaming(string boneName, List<BoneNode> nodes)
        {
            // For finger bones like j_index_le_1, parent is j_wrist_le or j_index_le_0
            if (boneName.Contains("_le_") || boneName.EndsWith("_le"))
            {
                var wrist = nodes.FirstOrDefault(n => n.Name.Equals("j_wrist_le", StringComparison.OrdinalIgnoreCase));
                if (wrist != null) return wrist;
            }
            if (boneName.Contains("_ri_") || boneName.EndsWith("_ri"))
            {
                var wrist = nodes.FirstOrDefault(n => n.Name.Equals("j_wrist_ri", StringComparison.OrdinalIgnoreCase));
                if (wrist != null) return wrist;
            }

            // For numbered bones, find the previous number
            var match = System.Text.RegularExpressions.Regex.Match(boneName, @"(.+)_(\d+)$");
            if (match.Success)
            {
                string baseName = match.Groups[1].Value;
                int num = int.Parse(match.Groups[2].Value);
                if (num > 0)
                {
                    var parent = nodes.FirstOrDefault(n =>
                        n.Name.Equals($"{baseName}_{num - 1}", StringComparison.OrdinalIgnoreCase));
                    if (parent != null) return parent;
                }
                // Try base name without number
                var baseParent = nodes.FirstOrDefault(n =>
                    n.Name.Equals(baseName, StringComparison.OrdinalIgnoreCase));
                if (baseParent != null) return baseParent;
            }

            return null;
        }

        /// <summary>
        /// Gets a reasonable offset for a bone based on its name.
        /// </summary>
        private Vector3 GetBoneOffset(string boneName)
        {
            var lower = boneName.ToLowerInvariant();

            // Approximate offsets based on typical humanoid proportions
            if (lower.Contains("head")) return new Vector3(0, 10, 0);
            if (lower.Contains("neck")) return new Vector3(0, 8, 0);
            if (lower.Contains("spine")) return new Vector3(0, 15, 0);
            if (lower.Contains("hip") && !lower.Contains("_le") && !lower.Contains("_ri"))
                return new Vector3(0, 5, 0);

            // Arms - left
            if (lower.Contains("clavicle_le")) return new Vector3(-5, 5, 0);
            if (lower.Contains("shoulder_le")) return new Vector3(-10, 0, 0);
            if (lower.Contains("elbow_le")) return new Vector3(-15, 0, 0);
            if (lower.Contains("wrist_le")) return new Vector3(-12, 0, 0);

            // Arms - right
            if (lower.Contains("clavicle_ri")) return new Vector3(5, 5, 0);
            if (lower.Contains("shoulder_ri")) return new Vector3(10, 0, 0);
            if (lower.Contains("elbow_ri")) return new Vector3(15, 0, 0);
            if (lower.Contains("wrist_ri")) return new Vector3(12, 0, 0);

            // Legs - left
            if (lower.Contains("hip_le")) return new Vector3(-5, -5, 0);
            if (lower.Contains("knee_le")) return new Vector3(0, -20, 0);
            if (lower.Contains("ankle_le")) return new Vector3(0, -20, 0);
            if (lower.Contains("ball_le")) return new Vector3(0, -2, 5);

            // Legs - right
            if (lower.Contains("hip_ri")) return new Vector3(5, -5, 0);
            if (lower.Contains("knee_ri")) return new Vector3(0, -20, 0);
            if (lower.Contains("ankle_ri")) return new Vector3(0, -20, 0);
            if (lower.Contains("ball_ri")) return new Vector3(0, -2, 5);

            // Default small offset
            return new Vector3(0, 2, 0);
        }

        /// <summary>
        /// Writes the hierarchy recursively.
        /// </summary>
        private void WriteHierarchyRecursive(BoneNode bone, int indent, bool isRoot)
        {
            string prefix = new string('\t', indent);

            if (isRoot)
            {
                _builder.AppendLine($"{prefix}ROOT {SanitizeBoneName(bone.Name)}");
                _builder.AppendLine($"{prefix}{{");
                _builder.AppendLine($"{prefix}\tOFFSET 0.00 0.00 0.00");
                _builder.AppendLine($"{prefix}\tCHANNELS 6 Xposition Yposition Zposition Zrotation Xrotation Yrotation");
            }
            else
            {
                _builder.AppendLine($"{prefix}JOINT {SanitizeBoneName(bone.Name)}");
                _builder.AppendLine($"{prefix}{{");
                _builder.AppendLine($"{prefix}\tOFFSET {bone.Offset.X:F2} {bone.Offset.Y:F2} {bone.Offset.Z:F2}");
                _builder.AppendLine($"{prefix}\tCHANNELS 3 Zrotation Xrotation Yrotation");
            }

            if (bone.Children.Count > 0)
            {
                foreach (var child in bone.Children)
                {
                    WriteHierarchyRecursive(child, indent + 1, false);
                }
            }
            else
            {
                // Leaf bone - add End Site
                _builder.AppendLine($"{prefix}\tEnd Site");
                _builder.AppendLine($"{prefix}\t{{");
                _builder.AppendLine($"{prefix}\t\tOFFSET 0.00 2.00 0.00");
                _builder.AppendLine($"{prefix}\t}}");
            }

            _builder.AppendLine($"{prefix}}}");
        }

        /// <summary>
        /// Writes a single root bone with end site (for animations with no bones).
        /// </summary>
        private void WriteSingleRootBone(string name)
        {
            _builder.AppendLine($"ROOT {SanitizeBoneName(name)}");
            _builder.AppendLine("{");
            _builder.AppendLine("\tOFFSET 0.00 0.00 0.00");
            _builder.AppendLine("\tCHANNELS 6 Xposition Yposition Zposition Zrotation Xrotation Yrotation");
            _builder.AppendLine("\tEnd Site");
            _builder.AppendLine("\t{");
            _builder.AppendLine("\t\tOFFSET 0.00 1.00 0.00");
            _builder.AppendLine("\t}");
            _builder.AppendLine("}");
        }

        /// <summary>
        /// Helper class for building bone hierarchy.
        /// </summary>
        private class BoneNode
        {
            public string Name { get; set; } = "";
            public int Index { get; set; }
            public Vector3 Offset { get; set; }
            public BoneNode? Parent { get; set; }
            public List<BoneNode> Children { get; set; } = new List<BoneNode>();
        }

        /// <summary>
        /// Writes the MOTION section with frame data.
        /// </summary>
        private void WriteMotion()
        {
            int frameCount = _xanim.NumFrames > 0 ? _xanim.NumFrames : 1;
            int boneCount = Math.Max(_xanim.BoneNames.Count, 1);

            _builder.AppendLine("MOTION");
            _builder.AppendLine($"Frames: {frameCount}");
            _builder.AppendLine($"Frame Time: {_frameTime.ToString("F6", CultureInfo.InvariantCulture)}");

            // Write frame data
            for (int frame = 0; frame < frameCount; frame++)
            {
                WriteFrameData(frame, boneCount);
            }
        }

        /// <summary>
        /// Writes data for a single frame.
        /// </summary>
        private void WriteFrameData(int frame, int boneCount)
        {
            var values = new List<string>();

            // Root bone: 6 channels (position + rotation)
            // Position (Xpos, Ypos, Zpos)
            Vector3 rootPos = GetBonePosition(0, frame);
            values.Add(rootPos.X.ToString("F4", CultureInfo.InvariantCulture));
            values.Add(rootPos.Y.ToString("F4", CultureInfo.InvariantCulture));
            values.Add(rootPos.Z.ToString("F4", CultureInfo.InvariantCulture));

            // Root rotation (Zrot, Xrot, Yrot) in degrees
            Vector3 rootRot = GetBoneRotationEuler(0, frame);
            values.Add(rootRot.Z.ToString("F4", CultureInfo.InvariantCulture));
            values.Add(rootRot.X.ToString("F4", CultureInfo.InvariantCulture));
            values.Add(rootRot.Y.ToString("F4", CultureInfo.InvariantCulture));

            // Child bones: 3 channels each (rotation only)
            for (int bone = 1; bone < boneCount; bone++)
            {
                Vector3 rot = GetBoneRotationEuler(bone, frame);
                values.Add(rot.Z.ToString("F4", CultureInfo.InvariantCulture));
                values.Add(rot.X.ToString("F4", CultureInfo.InvariantCulture));
                values.Add(rot.Y.ToString("F4", CultureInfo.InvariantCulture));
            }

            _builder.AppendLine(string.Join(" ", values));
        }

        /// <summary>
        /// Gets the position for a bone at a specific frame.
        /// </summary>
        private Vector3 GetBonePosition(int boneIndex, int frame)
        {
            if (_extractedData?.Bones != null && boneIndex < _extractedData.Bones.Count)
            {
                var bone = _extractedData.Bones[boneIndex];
                if (bone.Translation?.Keys != null && bone.Translation.Keys.Count > 0)
                {
                    // Find the keyframe for this frame
                    foreach (var key in bone.Translation.Keys)
                    {
                        if (key.Frame >= frame)
                            return key.Position;
                    }
                    return bone.Translation.Keys[^1].Position;
                }
            }
            return Vector3.Zero;
        }

        /// <summary>
        /// Gets the rotation for a bone at a specific frame as Euler angles (degrees).
        /// </summary>
        private Vector3 GetBoneRotationEuler(int boneIndex, int frame)
        {
            Quaternion quat = Quaternion.Identity;

            if (_extractedData?.Bones != null && boneIndex < _extractedData.Bones.Count)
            {
                var bone = _extractedData.Bones[boneIndex];
                if (bone.Rotation?.Keys != null && bone.Rotation.Keys.Count > 0)
                {
                    // Find the keyframe for this frame
                    foreach (var key in bone.Rotation.Keys)
                    {
                        if (key.Frame >= frame)
                        {
                            quat = key.Rotation;
                            break;
                        }
                    }
                    if (quat == Quaternion.Identity && bone.Rotation.Keys.Count > 0)
                        quat = bone.Rotation.Keys[^1].Rotation;
                }
            }

            return QuaternionToEuler(quat);
        }

        /// <summary>
        /// Converts a quaternion to Euler angles (degrees) in ZXY order.
        /// </summary>
        private static Vector3 QuaternionToEuler(Quaternion q)
        {
            // Convert quaternion to Euler angles (ZXY order for BVH)
            float x, y, z;

            // Roll (X)
            float sinr_cosp = 2 * (q.W * q.X + q.Y * q.Z);
            float cosr_cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            x = MathF.Atan2(sinr_cosp, cosr_cosp);

            // Pitch (Y)
            float sinp = 2 * (q.W * q.Y - q.Z * q.X);
            if (MathF.Abs(sinp) >= 1)
                y = MathF.CopySign(MathF.PI / 2, sinp);
            else
                y = MathF.Asin(sinp);

            // Yaw (Z)
            float siny_cosp = 2 * (q.W * q.Z + q.X * q.Y);
            float cosy_cosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
            z = MathF.Atan2(siny_cosp, cosy_cosp);

            // Convert to degrees
            const float rad2deg = 180.0f / MathF.PI;
            return new Vector3(x * rad2deg, y * rad2deg, z * rad2deg);
        }

        /// <summary>
        /// Sanitizes a bone name for BVH format (no spaces or special chars).
        /// </summary>
        private static string SanitizeBoneName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "bone";
            return name.Replace(' ', '_').Replace('/', '_').Replace('\\', '_');
        }
    }

    /// <summary>
    /// Helper class for exporting XAnim to BVH format.
    /// </summary>
    public static class BVHExporter
    {
        /// <summary>
        /// Exports an XAnimParts to a BVH file.
        /// </summary>
        /// <param name="xanim">The XAnimParts to export.</param>
        /// <param name="filePath">The output file path.</param>
        /// <param name="zoneData">Optional zone data for keyframe extraction.</param>
        /// <returns>True if export succeeded, false otherwise.</returns>
        public static bool Export(XAnimParts xanim, string filePath, byte[]? zoneData = null)
        {
            try
            {
                // Extract animation data if zone data provided
                XAnimExtractedData? extractedData = null;
                if (zoneData != null)
                {
                    var parser = new ZoneParsers.XAnimDataParser(zoneData, isBigEndian: true);
                    extractedData = parser.ExtractAnimationData(xanim);
                }

                var writer = new BVHWriter(xanim, extractedData);
                string content = writer.Generate();

                File.WriteAllText(filePath, content, Encoding.ASCII);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Exports an XAnimParts to a BVH file with detailed error information.
        /// </summary>
        /// <param name="xanim">The XAnimParts to export.</param>
        /// <param name="filePath">The output file path.</param>
        /// <param name="zoneData">Optional zone data for keyframe extraction.</param>
        /// <param name="error">Error message if export fails.</param>
        /// <returns>True if export succeeded, false otherwise.</returns>
        public static bool Export(XAnimParts xanim, string filePath, byte[]? zoneData, out string? error)
        {
            error = null;

            try
            {
                // Extract animation data if zone data provided
                XAnimExtractedData? extractedData = null;
                if (zoneData != null)
                {
                    var parser = new ZoneParsers.XAnimDataParser(zoneData, isBigEndian: true);
                    extractedData = parser.ExtractAnimationData(xanim);
                }

                var writer = new BVHWriter(xanim, extractedData);
                string content = writer.Generate();

                File.WriteAllText(filePath, content, Encoding.ASCII);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
