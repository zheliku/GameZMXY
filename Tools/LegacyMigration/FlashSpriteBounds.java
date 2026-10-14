import com.jpexs.decompiler.flash.SWF;
import com.jpexs.decompiler.flash.tags.DefineSpriteTag;
import com.jpexs.decompiler.flash.types.RECT;
import java.io.FileInputStream;

/** Read the exact filtered PNG export rectangle instead of guessing padding. */
public class FlashSpriteBounds {
    public static void main(String[] args) throws Exception {
        SWF swf = new SWF(new FileInputStream(args[0]), true);
        for (int i = 1; i < args.length; i++) {
            int id = Integer.parseInt(args[i]);
            DefineSpriteTag sprite = (DefineSpriteTag) swf.getCharacter(id);
            RECT rect = sprite.getTimeline().getDisplayRectWithFilters();
            System.out.printf("%d %d %d %d %d%n", id, rect.Xmin, rect.Ymin, rect.Xmax, rect.Ymax);
        }
    }
}
