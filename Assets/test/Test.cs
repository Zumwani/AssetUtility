using System;

namespace DocumentationTest
{

    /// <summary>
    /// This is a class.
    /// <para>This is a paragraph.</para>
    /// <para>Test1 does the following:</para>
    /// <list type="bullet">
    ///     <item>
    ///         <term><see cref="Sak"/></term>
    ///         <description>Does sak.</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="Sak(string)"/></term>
    ///         <description>Does sak, but with parameter.</description>
    ///     </item>
    /// </list>
    /// <para>Usage:</para>
    /// <code>
    /// Test.Sak("sak");
    /// </code>
    /// </summary>
    /// <remarks>
    /// dasdok
    /// </remarks>
    /// <remarks>
    /// sap
    /// fdsf
    /// <para>sdasd</para>
    /// </remarks>
    public static class Test
    {

        /// <summary>This is a field.</summary>
        public static string test;

        /// <summary>Enables or disables sak.</summary>
        public static bool doSak { get; set; }

        /// <summary>Occurs when sak.</summary>
        public static event Action onSak;

        /// <summary>Does sak.</summary>
        public static void Sak()
        {

        }

        /// <summary>Does sak, but with <paramref name="stringParam"/>.</summary>
        /// <param name="stringParam">This is a param.</param>
        public static void Sak(string stringParam)
        {

        }

        /// <summary>This is a nested class.</summary>
        public static class NestedTest
        {

            /// <summary>This is a method in a nested class.</summary>
            public static void Sak()
            { }

        }

    }

    /// <summary>This is test2.</summary>
    /// <typeparam name="T">The generic type.</typeparam>
    /// <typeparam name="T2">The second generic type.</typeparam>
    public static class Test2<T, T2>
    {

        /// <summary>This is a method in a second class.</summary>
        public static void Sak()
        { }

    }

    /// <summary>Inherit test.</summary>
    public abstract class BaseClass
    {
        /// <summary>This is an abstract method.</summary>
        public abstract void sak<T>(string sak);
    }

    /// <summary>SubClass.</summary>
    public class Subclass : BaseClass
    {

        /// <inheritdoc/>
        public override void sak<T>(string sak)
        {

        }

    }

}
