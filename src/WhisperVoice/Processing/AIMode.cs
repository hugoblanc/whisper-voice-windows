namespace WhisperVoice.Processing;

/// <summary>
/// AI processing modes for post-transcription text enhancement
/// </summary>
public class AIMode
{
    public string Id { get; }
    public string Name { get; }
    public string? SystemPrompt { get; }

    /// <summary>
    /// Whether this mode requires AI processing (GPT-4o-mini)
    /// </summary>
    public bool RequiresProcessing => SystemPrompt != null;

    private AIMode(string id, string name, string? systemPrompt = null)
    {
        Id = id;
        Name = name;
        SystemPrompt = systemPrompt;
    }

    // Available modes - matching macOS implementation
    public static readonly AIMode Brut = new("voice-to-text", "Brut");

    public static readonly AIMode Clean = new("clean", "Clean", """
        Tu es un assistant qui nettoie des transcriptions vocales.
        Règles:
        - Supprime les hésitations (euh, hmm, ben, bah, genre, en fait répété)
        - Corrige la ponctuation et les majuscules
        - Garde le sens et le ton exact du message
        - Ne reformule PAS, ne résume PAS
        - Réponds UNIQUEMENT avec le texte corrigé, rien d'autre
        """);

    public static readonly AIMode Formal = new("formal", "Formel", """
        Tu es un assistant qui transforme des transcriptions vocales en texte professionnel.
        Règles:
        - Adopte un ton formel et professionnel
        - Corrige grammaire, ponctuation, majuscules
        - Structure le texte si nécessaire (paragraphes)
        - Garde le message original intact
        - Réponds UNIQUEMENT avec le texte transformé, rien d'autre
        """);

    public static readonly AIMode Casual = new("casual", "Casual", """
        Tu es un assistant qui nettoie des transcriptions vocales en gardant un ton décontracté.
        Règles:
        - Garde un ton naturel et amical
        - Supprime les hésitations excessives mais garde le naturel
        - Corrige les erreurs évidentes seulement
        - Préserve les expressions familières
        - Réponds UNIQUEMENT avec le texte nettoyé, rien d'autre
        """);

    public static readonly AIMode Markdown = new("markdown", "Markdown", """
        Tu es un assistant qui convertit des transcriptions vocales en Markdown structuré.
        Règles:
        - Utilise des headers (#, ##) si le contenu a une structure
        - Utilise des listes (-, *) pour les énumérations
        - Utilise **gras** pour les points importants
        - Utilise `code` pour les termes techniques
        - Corrige grammaire et ponctuation
        - Réponds UNIQUEMENT avec le texte en Markdown, rien d'autre
        """);

    /// <summary>
    /// All available modes in order
    /// </summary>
    public static readonly AIMode[] All = { Brut, Clean, Formal, Casual, Markdown };

    /// <summary>
    /// Get mode by ID
    /// </summary>
    public static AIMode? GetById(string id) =>
        Array.Find(All, m => m.Id == id);

    /// <summary>
    /// Get index of mode in All array
    /// </summary>
    public static int IndexOf(AIMode mode) =>
        Array.IndexOf(All, mode);
}
