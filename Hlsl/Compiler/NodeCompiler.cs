﻿using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Operations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public sealed class NodeCompiler
    {
        private readonly RegisterState _registers;
        private readonly NodeGrouper _nodeGrouper;
        private readonly ConstantCompiler _constantCompiler;
        private readonly MatrixMultiplicationCompiler _matrixMultiplicationCompiler;

        public const int PromoteToAnyVectorSize = -1;

        public NodeCompiler(RegisterState registers)
        {
            _registers = registers;
            _nodeGrouper = new NodeGrouper(registers);
            _constantCompiler = new ConstantCompiler();
            _matrixMultiplicationCompiler = new MatrixMultiplicationCompiler(this);
        }

        public string Compile(HlslTreeNode node)
        {
            return Compile(new List<HlslTreeNode>() { node });
        }

        public string Compile(IEnumerable<HlslTreeNode> group, int promoteToVectorSize = PromoteToAnyVectorSize)
        {
            return Compile(group.ToList(), promoteToVectorSize);
        }

        public string Compile(List<HlslTreeNode> components, int promoteToVectorSize = PromoteToAnyVectorSize)
        {
            if (components.Count == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(components));
            }

            if (components.Count > 1)
            {
                IList<IList<HlslTreeNode>> componentGroups = _nodeGrouper.GroupComponents(components);
                if (componentGroups.Count > 1)
                {
                    return CompileVectorConstructor(components, componentGroups);
                }

                var multiplication = _nodeGrouper.MatrixMultiplicationGrouper.TryGetMultiplicationGroup(components);
                if (multiplication != null)
                {
                    return _matrixMultiplicationCompiler.Compile(multiplication);
                }

                var normalize = _nodeGrouper.NormalizeGrouper.TryGetContext(components);
                if (normalize != null)
                {
                    var vector = Compile(normalize);
                    return $"normalize({vector})";
                }
            }

            var first = components[0];

            if (first is ConstantNode)
            {
                return CompileConstant(components, promoteToVectorSize);
            }

            if (first is Operation operation)
            {
                return CompileOperation(operation, components, promoteToVectorSize);
            }

            if (first is IHasComponentIndex)
            {
                return CompileNodesWithComponents(components, first, promoteToVectorSize);
            }

            if (first is GroupNode group)
            {
                return Compile(group.Inputs);
            }

            throw new NotImplementedException();
        }

        private string CompileVectorConstructor(List<HlslTreeNode> components, IList<IList<HlslTreeNode>> componentGroups)
        {
            UngroupConstantGroups(componentGroups);

            IEnumerable<string> compiledConstructorParts = componentGroups.Select(Compile);
            return $"float{components.Count}({string.Join(", ", compiledConstructorParts)})";
        }

        private static void UngroupConstantGroups(IList<IList<HlslTreeNode>> componentGroups)
        {
            int i = 0;
            while (i < componentGroups.Count)
            {
                var componentGroup = componentGroups[i];
                if (componentGroup.All(c => c is ConstantNode))
                {
                    componentGroups.RemoveAt(i);
                    foreach (var groupComponent in componentGroup)
                    {
                        componentGroups.Insert(i, new[] { groupComponent });
                        i++;
                    }
                }
                else
                {
                    i++;
                }
            }
        }

        private string CompileConstant(List<HlslTreeNode> components, int promoteToVectorSize)
        {
            var constantComponents = components.Cast<ConstantNode>().ToArray();
            return _constantCompiler.Compile(constantComponents);
        }

        private string CompileOperation(Operation operation, List<HlslTreeNode> components, int promoteToVectorSize)
        {
            switch (operation)
            {
                case NegateOperation _:
                    {
                        string name = operation.Mnemonic;
                        IEnumerable<HlslTreeNode> input = components.Select(g => g.Inputs[0]);
                        bool isAssociative = AssociativityTester.TestForMultiplication(input.First());
                        string value = Compile(input);
                        return isAssociative
                            ? $"-{value}"
                            : $"-({value})";
                    }

                case ConsumerOperation _:
                case SignGreaterOrEqualOperation _:
                case SignLessOperation _:
                    {
                        string name = operation.Mnemonic;
                        string value = Compile(components.Select(g => g.Inputs[0]));
                        return $"{name}({value})";
                    }

                case AddOperation _:
                    {
                        return string.Format("{0} + {1}",
                            Compile(components.Select(g => g.Inputs[0])),
                            Compile(components.Select(g => g.Inputs[1])));
                    }

                case SubtractOperation _:
                    {
                        return string.Format("{0} - {1}",
                            Compile(components.Select(g => g.Inputs[0])),
                            Compile(components.Select(g => g.Inputs[1])));
                    }

                case MultiplyOperation _:
                    {
                        var multiplicand1 = components.Select(g => g.Inputs[0]);
                        var multiplicand2 = components.Select(g => g.Inputs[1]);

                        if (!(multiplicand1.First() is ConstantNode) && multiplicand2.First() is ConstantNode)
                        {
                            var temp = multiplicand1;
                            multiplicand1 = multiplicand2;
                            multiplicand2 = temp;
                        }

                        bool firstIsAssociative = AssociativityTester.TestForMultiplication(multiplicand1.First());
                        bool secondIsAssociative = AssociativityTester.TestForMultiplication(multiplicand2.First());
                        string format =
                            (firstIsAssociative ? "{0}" : "({0})") +
                            " * " +
                            (secondIsAssociative ? "{1}" : "({1})");

                        return string.Format(format,
                            Compile(multiplicand1, promoteToVectorSize),
                            Compile(multiplicand2, promoteToVectorSize));
                    }

                case DivisionOperation _:
                    {
                        var dividend = components.Select(g => g.Inputs[0]);
                        var divisor = components.Select(g => g.Inputs[1]);

                        bool divisorIsAssociative = AssociativityTester.TestForMultiplication(divisor.First());
                        string format = divisorIsAssociative
                            ? "{0} / {1}"
                            : "{0} / ({1})";

                        return string.Format("{0} / {1}",
                            Compile(dividend),
                            Compile(divisor));
                    }

                case MaximumOperation _:
                case MinimumOperation _:
                case PowerOperation _:
                    {
                        var value1 = Compile(components.Select(g => g.Inputs[0]));
                        var value2 = Compile(components.Select(g => g.Inputs[1]));

                        var name = operation.Mnemonic;

                        return $"{name}({value1}, {value2})";
                    }

                case LinearInterpolateOperation _:
                    {
                        var value1 = Compile(components.Select(g => g.Inputs[0]));
                        var value2 = Compile(components.Select(g => g.Inputs[1]));
                        var value3 = Compile(components.Select(g => g.Inputs[2]));

                        var name = "lerp";

                        return $"{name}({value1}, {value2}, {value3})";
                    }

                case CompareOperation _:
                    {
                        var value1 = Compile(components.Select(g => g.Inputs[0]));
                        var value2 = Compile(components.Select(g => g.Inputs[1]), components.Count);
                        var value3 = Compile(components.Select(g => g.Inputs[2]), components.Count);

                        return $"{value1} >= 0 ? {value2} : {value3}";
                    }
                case DotProductOperation _:
                    {
                        var x = Compile(components.Select(g => g.Inputs[0]));
                        var y = Compile(components.Select(g => g.Inputs[1]));
                        return $"dot({x}, {y})";
                    }
                case LengthOperation _:
                    {
                        var value1 = Compile(components.Select(g => g.Inputs[0]));
                        return $"length({value1})";
                    }
                default:
                    throw new NotImplementedException();
            }
        }

        private string CompileNodesWithComponents(List<HlslTreeNode> components, HlslTreeNode first, int promoteToVectorSize)
        {
            var componentsWithIndices = components.Cast<IHasComponentIndex>();

            if (first is RegisterInputNode shaderInput)
            {
                var registerKey = shaderInput.RegisterComponentKey.RegisterKey;

                string swizzle = "";
                if (!(registerKey is D3D9RegisterKey d3D9RegisterKey && d3D9RegisterKey.Type == RegisterType.Sampler))
                {
                    swizzle = GetAstSourceSwizzleName(componentsWithIndices,
                        _registers.GetRegisterFullLength(registerKey),
                        promoteToVectorSize);
                }

                string name = _registers.GetRegisterName(registerKey);
                return $"{name}{swizzle}";
            }

            if (first is TextureLoadOutputNode textureLoad)
            {
                string swizzle = GetAstSourceSwizzleName(componentsWithIndices, 4);

                string sampler = Compile(new[] { textureLoad.SamplerInput });
                string texcoords = Compile(textureLoad.TextureCoordinateInputs);
                return $"tex2D({sampler}, {texcoords}){swizzle}";
            }

            if (first is NormalizeOutputNode)
            {
                string input = Compile(first.Inputs);
                string swizzle = GetAstSourceSwizzleName(componentsWithIndices, 4);
                return $"normalize({input}){swizzle}";
            }

            throw new NotImplementedException();
        }

        private static string GetAstSourceSwizzleName(IEnumerable<IHasComponentIndex> inputs,
            int registerSize, 
            int promoteToVectorSize = PromoteToAnyVectorSize)
        {
            string swizzleName = "";
            foreach (int swizzle in inputs.Select(i => i.ComponentIndex))
            {
                swizzleName += "xyzw"[swizzle];
            }

            if (swizzleName.Equals("xyzw".Substring(0, registerSize)))
            {
                return "";
            }

            if (promoteToVectorSize == PromoteToAnyVectorSize && swizzleName.Distinct().Count() == 1)
            {
                return "." + swizzleName.First();
            }

            return "." + swizzleName;
        }
    }
}
