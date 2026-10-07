using FluentAssertions;
using RepoLore.Core.Migration;
using Xunit;

namespace RepoLore.Core.Tests.Migration;

public class MigrationPlannerTests
{
    [Fact]
    public void Sparse_only_note_is_a_keep_candidate_at_its_destination()
    {
        var notes = new[]
        {
            V(NoteSource.Sparse, "_repolore/sparse-tree/src/src.md", "src/src.md", "SPARSE_ONLY")
        };

        var mappings = MigrationPlanner.Classify(notes);

        mappings.Should().ContainSingle();
        mappings[0].Status.Should().Be(MappingStatus.Candidate);
        mappings[0].Destination.Should().Be("_repolore/sparse-tree/src/src.md");
        mappings[0].SourcePath.Should().BeNull("the sparse copy is already at its destination");
    }

    [Fact]
    public void Local_only_tree_note_copies_to_the_sparse_destination()
    {
        var notes = new[]
        {
            V(NoteSource.Tree, "_repolore/tree/src/src.md", "src/src.md", "TREE_ONLY")
        };

        var mappings = MigrationPlanner.Classify(notes);

        mappings.Should().ContainSingle();
        mappings[0].Status.Should().Be(MappingStatus.Candidate);
        mappings[0].Destination.Should().Be("_repolore/sparse-tree/src/src.md");
        mappings[0].SourcePath.Should().Be("_repolore/tree/src/src.md");
    }

    [Fact]
    public void Identical_tree_and_sparse_copies_are_a_single_candidate_without_copy()
    {
        var notes = new[]
        {
            V(NoteSource.Tree, "_repolore/tree/src/src.md", "src/src.md", "SAME"),
            V(NoteSource.Sparse, "_repolore/sparse-tree/src/src.md", "src/src.md", "SAME")
        };

        var mappings = MigrationPlanner.Classify(notes);

        mappings.Should().ContainSingle();
        mappings[0].Status.Should().Be(MappingStatus.Candidate);
        mappings[0].SourcePath.Should().BeNull("sparse wins when identical, so no copy is needed");
    }

    [Fact]
    public void Differing_useful_copies_are_a_conflict()
    {
        var notes = new[]
        {
            V(NoteSource.Tree, "_repolore/tree/src/src.md", "src/src.md", "TREE_VALUE"),
            V(NoteSource.Sparse, "_repolore/sparse-tree/src/src.md", "src/src.md", "SPARSE_VALUE")
        };

        var mappings = MigrationPlanner.Classify(notes);

        mappings.Should().ContainSingle();
        mappings[0].Status.Should().Be(MappingStatus.Conflict);
    }

    [Fact]
    public void Empty_markers_are_ignored_when_the_other_variant_is_useful()
    {
        var notes = new[]
        {
            V(NoteSource.Tree, "_repolore/tree/src/src.md", "src/src.md", "TREE_VALUE"),
            V(NoteSource.Sparse, "_repolore/sparse-tree/src/src.md", "src/src.md", "<!-- repolore:empty -->")
        };

        var mappings = MigrationPlanner.Classify(notes);

        mappings.Should().ContainSingle();
        mappings[0].Status.Should().Be(MappingStatus.Candidate);
        mappings[0].SourcePath.Should().Be("_repolore/tree/src/src.md");
    }

    [Fact]
    public void Ambiguous_old_naming_is_a_conflict_even_when_a_source_directory_exists()
    {
        var notes = new[]
        {
            V(NoteSource.Tree, "_repolore/tree/foo/bar.md", "foo/bar.md", "MISNAMED")
        };

        var mappings = MigrationPlanner.Classify(notes);

        mappings.Should().ContainSingle();
        mappings[0].Status.Should().Be(MappingStatus.Ambiguous);
    }

    [Fact]
    public void Root_note_keeps_an_existing_top_level_root()
    {
        var notes = new[]
        {
            V(NoteSource.TopRoot, "_repolore/root.md", "root.md", "ROOT_VALUE")
        };

        var mappings = MigrationPlanner.Classify(notes);

        mappings.Should().ContainSingle();
        mappings[0].Status.Should().Be(MappingStatus.Candidate);
        mappings[0].Destination.Should().Be("_repolore/root.md");
        mappings[0].SourcePath.Should().BeNull("the top-level root is already at its destination");
    }

    [Fact]
    public void Tree_root_copies_to_the_escaping_top_level_destination()
    {
        var notes = new[]
        {
            V(NoteSource.Tree, "_repolore/tree/root.md", "root.md", "TREE_ROOT")
        };

        var mappings = MigrationPlanner.Classify(notes);

        mappings.Should().ContainSingle();
        mappings[0].Status.Should().Be(MappingStatus.Candidate);
        mappings[0].Destination.Should().Be("_repolore/root.md");
        mappings[0].SourcePath.Should().Be("_repolore/tree/root.md");
    }

    [Fact]
    public void Differing_root_variants_are_a_conflict()
    {
        var notes = new[]
        {
            V(NoteSource.Tree, "_repolore/tree/root.md", "root.md", "TREE_ROOT"),
            V(NoteSource.TopRoot, "_repolore/root.md", "root.md", "TOP_ROOT")
        };

        var mappings = MigrationPlanner.Classify(notes);

        mappings.Should().ContainSingle();
        mappings[0].Status.Should().Be(MappingStatus.Conflict);
    }

    private static NoteVariant V(NoteSource source, string physicalPath, string alphaRelative, string normalizedText) =>
        new(source, physicalPath, alphaRelative, normalizedText);
}
